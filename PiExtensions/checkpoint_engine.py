"""Private, bounded workspace snapshots. Invoked through the checkpoint extension only."""
import contextlib
import difflib
import hashlib
import json
import os
from pathlib import Path
import stat
import subprocess
import sys
import time
import uuid

MAX_FILE = 8 * 1024 * 1024
MAX_TOTAL = 1024 * 1024 * 1024
MAX_FILES = 20000
EXCLUDED = {'.git', '.jj', 'node_modules', 'dist', 'build', 'bin', 'obj', '.cache', '.next', '.turbo', 'coverage', '.venv', '__pycache__'}


def atomic_json(path, value):
    temporary = path.with_name(path.name + '.' + uuid.uuid4().hex + '.tmp')
    try:
        with temporary.open('x', encoding='utf-8') as stream:
            json.dump(value, stream, ensure_ascii=True)
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary, path)
    finally:
        temporary.unlink(missing_ok=True)


class CheckpointEngine:
    def __init__(self, root, session, storage=None):
        self.root = Path(root).resolve(strict=True)
        if not self.root.is_dir() or self.root == self.root.parent or self.root == Path.home().resolve():
            raise ValueError('Checkpoints require a project folder, not a filesystem root or home folder.')
        self.session = hashlib.sha256(session.encode()).hexdigest()
        base = Path(storage) if storage else Path.home() / '.pi-desktop-checkpoints'
        base = base.resolve()
        if base == self.root or self.root in base.parents:
            raise ValueError('Checkpoint storage must be outside the workspace.')
        base.mkdir(parents=True, exist_ok=True, mode=0o700)
        self.base = base
        self.store = base / hashlib.sha256(os.path.normcase(str(self.root)).encode()).hexdigest()
        self.store.mkdir(exist_ok=True, mode=0o700)
        self.blobs = self.store / 'blobs'
        self.blobs.mkdir(exist_ok=True, mode=0o700)
        self.records = self.store / 'records'
        self.records.mkdir(exist_ok=True, mode=0o700)

    @contextlib.contextmanager
    def lock(self):
        # An OS-held target-side lock survives transport interruption until the helper exits.
        with (self.base / 'operation.lock').open('a+b') as stream:
            if os.name == 'nt':
                import msvcrt
                stream.seek(0)
                stream.write(b'0')
                stream.flush()
                stream.seek(0)
                msvcrt.locking(stream.fileno(), msvcrt.LK_NBLCK, 1)
            else:
                import fcntl
                fcntl.flock(stream.fileno(), fcntl.LOCK_EX | fcntl.LOCK_NB)
            try:
                yield
            finally:
                if os.name == 'nt':
                    stream.seek(0)
                    msvcrt.locking(stream.fileno(), msvcrt.LK_UNLCK, 1)

    def path(self, relative):
        if not isinstance(relative, str) or not relative or '\\' in relative or ':' in relative or any(ord(c) < 32 for c in relative):
            raise ValueError('Unsupported checkpoint path.')
        parts = relative.split('/')
        if any(p in ('', '.', '..') or p.rstrip(' .') != p for p in parts):
            raise ValueError('Invalid checkpoint path.')
        target = self.root
        for part in parts:
            target = target / part
            if target.is_symlink() or (hasattr(target, 'is_junction') and target.is_junction()):
                raise ValueError('Linked paths cannot be restored.')
            try:
                info = target.lstat()
                if getattr(info, 'st_file_attributes', 0) & 0x400:
                    raise ValueError('Reparse points cannot be restored.')
            except FileNotFoundError:
                pass
        if self.root not in target.resolve().parents:
            raise ValueError('Path leaves the workspace.')
        return target

    def read(self, relative, save=False):
        target = self.path(relative)
        try:
            before = target.stat()
        except FileNotFoundError:
            return None
        if not stat.S_ISREG(before.st_mode) or before.st_size > MAX_FILE:
            raise ValueError('Unsupported file kind or file exceeds 8 MiB.')
        with target.open('rb') as stream:
            data = stream.read(MAX_FILE + 1)
            after = os.fstat(stream.fileno())
        current = target.stat()
        identity = lambda s: (s.st_dev, s.st_ino, s.st_size, s.st_mtime_ns, s.st_mode)
        if len(data) > MAX_FILE or identity(before) != identity(after) or identity(after) != identity(current):
            raise ValueError('File changed during capture.')
        digest = hashlib.sha256(data).hexdigest()
        if save and not (self.blobs / digest).exists():
            if self.used + len(data) > MAX_TOTAL:
                raise ValueError('Checkpoint storage reached its 1 GiB limit. Existing recovery data is preserved.')
            with (self.blobs / digest).open('xb') as stream:
                stream.write(data)
                stream.flush()
                os.fsync(stream.fileno())
            self.used += len(data)
        return {'hash': digest, 'mode': stat.S_IMODE(before.st_mode)}

    def inventory(self):
        paths, omitted, ignored = [], [], set()
        errors = []
        started = time.monotonic()
        for directory, folders, files in os.walk(self.root, followlinks=False, onerror=errors.append):
            if time.monotonic() - started > 30:
                raise ValueError('Workspace exceeds the 30 second scan limit.')
            folders[:] = [f for f in folders if f not in EXCLUDED and not f.startswith('.pi-desktop-checkpoints')]
            for name in list(folders):
                relative = (Path(directory) / name).relative_to(self.root).as_posix()
                try:
                    self.path(relative)
                except ValueError:
                    folders.remove(name)
                    omitted.append(relative)
            for name in files:
                relative = (Path(directory) / name).relative_to(self.root).as_posix()
                if name == '.env' or name.startswith('.env.') or name.endswith(('.pem', '.key')):
                    continue
                paths.append(relative)
                if len(paths) > MAX_FILES or time.monotonic() - started > 30:
                    raise ValueError('Workspace exceeds checkpoint scan limits (20,000 files / 30 seconds).')
        if errors:
            raise ValueError('A workspace directory could not be scanned. Checkpoint coverage is unavailable.')
        # Git applies the project's ignore rules, independently of its index state.
        probe = subprocess.run(['git', '-C', str(self.root), 'rev-parse', '--show-toplevel'], capture_output=True, timeout=10)
        if probe.returncode == 0 and paths:
            check = subprocess.run(['git', '-C', str(self.root), 'check-ignore', '--no-index', '-z', '--stdin'],
                                   input=('\0'.join(paths) + '\0').encode(), capture_output=True, timeout=30)
            if check.returncode not in (0, 1):
                raise ValueError('Could not evaluate Git ignore rules.')
            ignored = set(check.stdout.decode('utf-8', errors='strict').split('\0'))
            paths = [p for p in paths if p not in ignored]
        return paths, omitted, sorted(ignored - {''})

    def capture(self):
        paths, omitted, excluded = self.inventory()
        files = {}
        for relative in paths:
            try:
                value = self.read(relative, True)
                if value is None:
                    raise ValueError('File disappeared during capture.')
                files[relative] = value
            except (OSError, ValueError):
                omitted.append(relative)
        return {'files': files, 'omitted': omitted[:1000], 'excluded': excluded}

    def prune(self):
        referenced = set()
        records = [(file, json.loads(file.read_text(encoding='utf-8'))) for file in self.records.glob('*.json')]
        by_session = {}
        for _, value in records:
            if value['state'] == 'complete' and not value.get('applied'):
                by_session.setdefault(value['session'], []).append(value)
        superseded = {value['id'] for values in by_session.values()
                      for value in sorted(values, key=lambda r: r['created'], reverse=True)[5:]}
        def collect(value):
            if isinstance(value, dict):
                if isinstance(value.get('hash'), str):
                    referenced.add(value['hash'])
                for child in value.values():
                    collect(child)
            elif isinstance(value, list):
                for child in value:
                    collect(child)
        for file, value in records:
            if value['id'] in superseded or (value['state'] not in ('active', 'applying', 'interrupted', 'partial', 'expired') and value['created'] < time.time() - 30 * 86400):
                value['cachedManifest'] = self.manifest(value)
                value['cachedManifest']['state'] = 'expired'
                value['cachedManifest']['applied'] = []
                value['state'] = 'expired'
                for field in ('before', 'after', 'journal', 'pending', 'applied'):
                    value.pop(field, None)
                self.save(value)
            collect(value)
        for blob in self.blobs.iterdir():
            if blob.name not in referenced:
                blob.unlink()

    def load(self, ident):
        if not isinstance(ident, str) or len(ident) != 32 or any(c not in '0123456789abcdef' for c in ident):
            raise ValueError('Invalid checkpoint identity.')
        value = json.loads((self.records / (ident + '.json')).read_text(encoding='utf-8'))
        if value['session'] != self.session:
            raise ValueError('Checkpoint belongs to another conversation.')
        return value

    def save(self, value):
        atomic_json(self.records / (value['id'] + '.json'), value)

    def active_records(self):
        # All workspace directories share the target-side coordinator. Overlap blocks restore.
        for directory in self.base.iterdir():
            if not directory.is_dir():
                continue
            metadata = directory / 'workspace.json'
            if not metadata.exists():
                continue
            other = Path(json.loads(metadata.read_text())['root'])
            if other != self.root and other not in self.root.parents and self.root not in other.parents:
                continue
            for record_path in (directory / 'records').glob('*.json'):
                record = json.loads(record_path.read_text(encoding='utf-8'))
                if record['state'] in ('active', 'applying', 'interrupted'):
                    yield record_path, record

    def changes(self, record):
        before, after = record['before']['files'], record.get('after', {}).get('files', {})
        excluded = set(record['before']['omitted'] + record.get('after', {}).get('omitted', []))
        excluded.update(record['before'].get('excluded', []))
        excluded.update(record.get('after', {}).get('excluded', []))
        result = []
        for relative in sorted(set(before) | set(after)):
            if any(relative == path or relative.startswith(path + '/') for path in excluded) or before.get(relative) == after.get(relative):
                continue
            a, b = before.get(relative), after.get(relative)
            patch, added, removed = None, 0, 0
            try:
                left = self.content(a)
                right = self.content(b)
                if max(len(left), len(right)) <= 256 * 1024 and b'\0' not in left + right:
                    lines = list(difflib.unified_diff(left.decode('utf-8').splitlines(True), right.decode('utf-8').splitlines(True),
                                                    fromfile='a/' + relative, tofile='b/' + relative))
                    if len(lines) < 5000:
                        patch = ''.join(line if line.endswith('\n') else line + '\n\\ No newline at end of file\n' for line in lines)
                        added = sum(l.startswith('+') and not l.startswith('+++') for l in lines)
                        removed = sum(l.startswith('-') and not l.startswith('---') for l in lines)
            except (UnicodeError, OSError):
                pass
            result.append({'path': relative, 'kind': 'created' if a is None else 'deleted' if b is None else 'modified',
                           'patch': patch, 'added': added, 'removed': removed,
                           'beforeHash': a.get('hash') if a else None, 'afterHash': b.get('hash') if b else None})
        # Bound the transport independently of snapshot coverage.
        budget = 512 * 1024
        for item in result:
            size = len((item['patch'] or '').encode())
            if size > budget:
                item['patch'] = None
            else:
                budget -= size
        return result

    def content(self, entry):
        if entry is None:
            return b''
        digest = entry['hash']
        if len(digest) != 64 or any(c not in '0123456789abcdef' for c in digest):
            raise ValueError('Invalid snapshot content identity.')
        data = (self.blobs / digest).read_bytes()
        if hashlib.sha256(data).hexdigest() != digest:
            raise ValueError('Snapshot content failed integrity verification.')
        return data

    def manifest(self, record):
        if record['state'] == 'expired':
            return record['cachedManifest']
        return {'id': record['id'], 'response': record.get('response', ''), 'state': record['state'],
                'overlap': record.get('overlap', False), 'omitted': len(record['before']['omitted']) + len(record.get('after', {}).get('omitted', [])),
                'files': self.changes(record) if 'after' in record else [], 'applied': record.get('applied', [])}

    def preview(self, record, undo=False):
        if record['state'] not in ('complete', 'reverted', 'partial', 'interrupted'):
            raise ValueError('This checkpoint is not ready for restoration.')
        if any(active['id'] != record['id'] for _, active in self.active_records()):
            raise ValueError('Another operation is active in this workspace. Finish it before reverting.')
        if record.get('overlap'):
            raise ValueError('Overlapping conversations changed this workspace. Changes cannot be safely attributed.')
        if record['before']['omitted'] or record.get('after', {}).get('omitted'):
            raise ValueError('This checkpoint has incomplete coverage and cannot be safely reverted.')
        source = record.get('journal', {}).get('original', {}) if undo else record['before']['files']
        expected = record['before']['files'] if undo else record['after']['files']
        paths = record.get('applied', []) if undo else [f['path'] for f in self.changes(record)]
        files = []
        for relative in paths:
            reason = ''
            try:
                self.content(source.get(relative))
                if self.read(relative) != expected.get(relative):
                    reason = 'File changed after this operation.'
            except (ValueError, OSError) as error:
                reason = str(error)
            if not undo and relative in record.get('applied', []):
                reason = 'Already reverted.'
            files.append({'path': relative, 'safe': not reason, 'reason': reason})
        return {'id': record['id'], 'undo': undo, 'files': files}

    def replace(self, relative, expected, desired):
        target = self.path(relative)
        data = self.content(desired)
        if self.read(relative) != expected:
            raise ValueError('File changed since preview; no overwrite performed.')
        if desired is None:
            target.unlink()
            return
        target.parent.mkdir(parents=True, exist_ok=True)
        self.path(relative)
        temporary = target.with_name('.pi-checkpoint-' + uuid.uuid4().hex)
        try:
            with temporary.open('xb') as stream:
                stream.write(data)
                stream.flush()
                os.fsync(stream.fileno())
            os.chmod(temporary, desired['mode'])
            if self.read(relative) != expected:
                raise ValueError('File changed during restore; no overwrite performed.')
            if expected is None:
                # No replace for creations: publishing must fail if another writer created the path.
                os.link(temporary, target)
            else:
                os.replace(temporary, target)
        finally:
            temporary.unlink(missing_ok=True)

    def apply(self, record, selected, undo=False):
        preview = self.preview(record, undo)
        allowed = {f['path'] for f in preview['files'] if f['safe']}
        if not selected or len(selected) != len(set(selected)) or not set(selected) <= allowed:
            raise ValueError('Selection is stale or includes conflicted files. Preview again.')
        original = {p: self.read(p, True) for p in selected}
        desired = record.get('journal', {}).get('original', {}) if undo else record['before']['files']
        desired = {p: desired.get(p) for p in selected}
        for entry in desired.values():
            self.content(entry)
        # Journal is durable before any mutation. Keep the previous revert journal for redo recovery.
        journal = {'original': original, 'desired': desired, 'selected': selected, 'done': [], 'undo': undo}
        if not undo:
            previous = record.get('journal', {}).get('original', {})
            record['journal'] = {'original': {**previous, **original}}
        record['pending'] = journal
        record['state'] = 'applying'
        self.save(record)
        error = ''
        try:
            for relative in selected:
                journal['current'] = relative
                self.save(record)
                self.replace(relative, original[relative], desired[relative])
                journal['done'].append(relative)
                record['applied'] = [p for p in record.get('applied', []) if p != relative] if undo else list(dict.fromkeys(record.get('applied', []) + [relative]))
                journal.pop('current', None)
                self.save(record)
        except (OSError, ValueError) as failure:
            error = str(failure)
        record['state'] = 'partial' if error else 'complete' if undo else 'reverted'
        record.pop('pending', None)
        self.save(record)
        return {'done': journal['done'], 'error': error, 'manifest': self.manifest(record)}

    def run(self, request):
        with self.lock():
            if request['action'] == 'clear':
                return self.clear_snapshot_data()
            if request['action'] == 'forget':
                return self.forget_session()
            atomic_json(self.store / 'workspace.json', {'root': str(self.root)})
            self.prune()
            self.used = sum(p.stat().st_size for p in self.base.glob('*/blobs/*') if p.is_file())
            action = request['action']
            if action == 'begin':
                if any(active['state'] in ('applying', 'interrupted') for _, active in self.active_records()):
                    raise ValueError('Inspect interrupted restoration before capturing more changes.')
                record = {'id': uuid.uuid4().hex, 'session': self.session, 'state': 'active', 'created': time.time(), 'before': self.capture(), 'overlap': request.get('overlap', False)}
                for file, active in self.active_records():
                    active['overlap'] = record['overlap'] = True
                    atomic_json(file, active)
                self.save(record)
                return {'id': record['id']}
            if action == 'list':
                records = []
                for file in self.records.glob('*.json'):
                    record = json.loads(file.read_text(encoding='utf-8'))
                    if record['session'] == self.session:
                        records.append(record)
                return {'records': [{'id': r['id'], 'response': r.get('response', ''), 'state': r['state']} for r in records]}
            record = self.load(request['id'])
            if action == 'finish':
                if record['state'] != 'active':
                    raise ValueError('Checkpoint already finalized.')
                record.update(after=self.capture(), response=request['response'], state='complete', overlap=record.get('overlap', False) or request.get('overlap', False))
                self.save(record)
                self.prune()
                return self.manifest(record)
            if action == 'manifest':
                return self.manifest(record)
            if action == 'preview':
                return self.preview(record, request.get('undo', False))
            if action == 'apply':
                return self.apply(record, request['paths'], request.get('undo', False))
            if action == 'recover':
                # Reconcile interrupted writes by content; never replay them automatically.
                pending = record.get('pending')
                if pending:
                    for relative in pending['selected']:
                        current = self.read(relative)
                        if current == pending['desired'].get(relative):
                            if pending['undo']:
                                record['applied'] = [p for p in record.get('applied', []) if p != relative]
                            else:
                                record['applied'] = list(dict.fromkeys(record.get('applied', []) + [relative]))
                        elif current != pending['original'].get(relative):
                            raise ValueError('Recovery found later file changes. Files were kept; resolve them before recovery.')
                    record.pop('pending', None)
                    record['state'] = 'partial'
                elif record['state'] == 'active':
                    record.update(after=self.capture(), response=request.get('response', ''), state='complete', overlap=True)
                self.save(record)
                return self.manifest(record)
            raise ValueError('Unsupported checkpoint operation.')

    def forget_session(self):
        matches = []
        for file in self.base.glob('*/records/*.json'):
            if any(path.is_symlink() or (hasattr(path, 'is_junction') and path.is_junction())
                   for path in (file, file.parent, file.parent.parent)):
                raise ValueError('Linked checkpoint storage cannot be removed.')
            record = json.loads(file.read_text(encoding='utf-8'))
            if record['session'] != self.session:
                continue
            if record['state'] in ('applying', 'interrupted', 'partial'):
                raise ValueError('Pending file restoration must be resolved before deleting its recovery data.')
            matches.append(file)
        original_records, original_blobs = self.records, self.blobs
        try:
            for directory in {file.parent for file in matches}:
                self.records, self.blobs = directory, directory.parent / 'blobs'
                if self.blobs.is_symlink() or (hasattr(self.blobs, 'is_junction') and self.blobs.is_junction()):
                    raise ValueError('Linked checkpoint storage cannot be removed.')
                for file in matches:
                    if file.parent == directory:
                        file.unlink()
                self.prune()
        finally:
            self.records, self.blobs = original_records, original_blobs
        return {'cleared': len(matches)}

    def clear_snapshot_data(self):
        # Preflight all stores before removing anything. Leave the coordinator and
        # expired manifests intact so historical cards retain their file summaries.
        stores = []
        for directory in self.base.iterdir():
            if not directory.is_dir():
                continue
            if directory.is_symlink() or (hasattr(directory, 'is_junction') and directory.is_junction()):
                raise ValueError('Linked checkpoint storage cannot be cleared.')
            if len(directory.name) != 64 or any(c not in '0123456789abcdef' for c in directory.name):
                raise ValueError('Unexpected checkpoint storage folder. Nothing was cleared.')
            records = directory / 'records'
            blobs = directory / 'blobs'
            for folder in (records, blobs):
                if folder.is_symlink() or (hasattr(folder, 'is_junction') and folder.is_junction()):
                    raise ValueError('Linked checkpoint storage cannot be cleared.')
            values = []
            for file in records.glob('*.json'):
                if file.is_symlink():
                    raise ValueError('Linked checkpoint records cannot be cleared.')
                record = json.loads(file.read_text(encoding='utf-8'))
                if record['state'] not in ('complete', 'reverted', 'expired'):
                    raise ValueError('Finish active captures and resolve pending or partial recovery before clearing data.')
                values.append((file, record))
            content_files = list(blobs.iterdir()) if blobs.exists() else []
            for file in content_files:
                if file.is_symlink() or not file.is_file() or len(file.name) != 64 or any(c not in '0123456789abcdef' for c in file.name):
                    raise ValueError('Unexpected checkpoint content. Nothing was cleared.')
            stores.append((blobs, values, content_files))
        manifests = []
        original_blobs = self.blobs
        try:
            for blobs, values, _ in stores:
                self.blobs = blobs
                for file, record in values:
                    manifest = self.manifest(record)
                    manifest.update(state='expired', applied=[])
                    manifests.append((file, {'id': record['id'], 'session': record['session'], 'created': record['created'],
                                             'state': 'expired', 'cachedManifest': manifest}))
        finally:
            self.blobs = original_blobs
        # Expire references first. Interruption may retain unused bytes, never a
        # usable checkpoint whose recovery bytes have already been removed.
        for file, value in manifests:
            atomic_json(file, value)
        removed = 0
        for _, _, files in stores:
            for file in files:
                removed += file.stat().st_size
                file.unlink()
        return {'cleared': len(manifests), 'bytes': removed}


def main():
    try:
        request = json.load(sys.stdin)
        engine = CheckpointEngine(request['root'], request['session'])
        result = engine.run(request)
        print(json.dumps({'ok': True, 'data': result}, ensure_ascii=True))
    except Exception as error:
        print(json.dumps({'ok': False, 'error': str(error)}, ensure_ascii=True))


if __name__ == '__main__':
    main()
