import importlib.util
import json
from pathlib import Path
import tempfile
import subprocess
import unittest
import threading
from unittest.mock import patch

spec = importlib.util.spec_from_file_location('checkpoint_engine', Path(__file__).parents[2] / 'PiExtensions' / 'checkpoint_engine.py')
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class CheckpointTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name) / 'project'
        self.root.mkdir()
        self.store = Path(self.temp.name) / 'history'
        self.engine = module.CheckpointEngine(str(self.root), 'session-a', self.store)

    def tearDown(self):
        self.temp.cleanup()

    def begin(self):
        return self.engine.run({'action': 'begin'})['id']

    def test_lock_contention_times_out_without_modifying_lock_file(self):
        with self.engine.lock():
            before = (self.store / 'operation.lock').stat().st_size
            with self.assertRaisesRegex(TimeoutError, 'busy in another chat'):
                with self.engine.lock(timeout=0.05):
                    self.fail('Concurrent lock acquired')
            self.assertEqual(before, (self.store / 'operation.lock').stat().st_size)

    def test_lock_waits_until_holder_releases(self):
        acquired = threading.Event()
        release = threading.Event()
        def holder():
            with self.engine.lock():
                acquired.set()
                release.wait(2)
        thread = threading.Thread(target=holder)
        thread.start()
        try:
            self.assertTrue(acquired.wait(2))
            timer = threading.Timer(0.1, release.set)
            timer.start()
            with self.engine.lock(timeout=1):
                self.assertTrue(release.is_set())
            timer.join()
        finally:
            release.set()
            thread.join()

    def test_lock_releases_after_exception(self):
        with self.assertRaisesRegex(ValueError, 'operation failed'):
            with self.engine.lock():
                raise ValueError('operation failed')
        with self.engine.lock(timeout=0):
            pass

    def test_lock_open_permission_failure_is_not_retried(self):
        with patch.object(Path, 'open', side_effect=PermissionError(13, 'denied')) as opened:
            with self.assertRaises(PermissionError):
                with self.engine.lock():
                    pass
            self.assertEqual(opened.call_count, 1)

    @unittest.skipUnless(module.os.name == 'nt', 'Windows lock error mapping')
    def test_windows_lock_access_denied_is_not_contention(self):
        with patch('ctypes.WinDLL') as library, patch('ctypes.get_last_error', return_value=5):
            kernel = library.return_value
            kernel.LockFile.return_value = False
            with self.assertRaises(PermissionError):
                with self.engine.lock():
                    pass
            self.assertEqual(kernel.LockFile.call_count, 1)
            kernel.UnlockFile.assert_not_called()

    def finish(self, ident):
        return self.engine.run({'action': 'finish', 'id': ident, 'response': 'assistant:123'})

    def write(self, name, data):
        (self.root / name).write_bytes(data)

    def apply(self, ident, paths, undo=False):
        return self.engine.run({'action': 'apply', 'id': ident, 'paths': paths, 'undo': undo})

    def test_changed_ignore_rules_do_not_invent_deletions(self):
        subprocess.run(['git', 'init', str(self.root)], check=True, capture_output=True)
        self.write('existing.txt', b'keep me')
        ident = self.begin()
        self.write('.gitignore', b'existing.txt\n')
        manifest = self.finish(ident)
        self.assertEqual([f['path'] for f in manifest['files']], ['.gitignore'])
        self.apply(ident, ['.gitignore'])
        self.assertEqual((self.root / 'existing.txt').read_bytes(), b'keep me')

    def test_clear_removes_snapshots_but_keeps_workspace_and_expired_summary(self):
        self.write('file', b'before')
        ident = self.begin()
        self.write('file', b'after')
        self.finish(ident)
        result = self.engine.run({'action': 'clear'})
        self.assertEqual(result['cleared'], 1)
        self.assertGreater(result['bytes'], 0)
        self.assertEqual(list(self.engine.blobs.iterdir()), [])
        self.assertEqual((self.root / 'file').read_bytes(), b'after')
        manifest = self.engine.run({'action': 'manifest', 'id': ident})
        self.assertEqual(manifest['state'], 'expired')
        self.assertEqual(manifest['files'][0]['path'], 'file')
        with self.assertRaisesRegex(ValueError, 'not ready'):
            self.engine.run({'action': 'preview', 'id': ident})
        fresh = self.begin()
        self.write('file', b'next')
        self.finish(fresh)
        self.apply(fresh, ['file'])
        self.assertEqual((self.root / 'file').read_bytes(), b'after')

    def test_clear_refuses_active_data_before_deleting_anything(self):
        self.write('file', b'before')
        ident = self.begin()
        self.write('file', b'after')
        self.finish(ident)
        other = module.CheckpointEngine(str(self.root), 'session-b', self.store)
        other.run({'action': 'begin'})
        with self.assertRaisesRegex(ValueError, 'pending'):
            self.engine.run({'action': 'clear'})
        self.assertTrue(list(self.engine.blobs.iterdir()))
        self.assertEqual(self.engine.load(ident)['state'], 'complete')

    def test_only_latest_five_completed_checkpoints_retain_bytes(self):
        ids = []
        for index in range(7):
            ident = self.begin()
            self.write('file', str(index).encode())
            self.finish(ident)
            ids.append(ident)
        self.assertEqual([self.engine.load(i)['state'] for i in ids], ['expired', 'expired'] + ['complete'] * 5)
        self.assertEqual(self.engine.manifest(self.engine.load(ids[0]))['files'][0]['kind'], 'created')

    def test_forget_deletes_only_selected_session_and_keeps_shared_blobs(self):
        self.write('file', b'shared')
        first = self.begin()
        self.write('file', b'after')
        self.finish(first)
        other = module.CheckpointEngine(str(self.root), 'session-b', self.store)
        second = other.run({'action': 'begin'})['id']
        self.write('file', b'other')
        other.run({'action': 'finish', 'id': second, 'response': 'assistant:456'})
        self.engine.run({'action': 'forget'})
        self.assertFalse((self.engine.records / (first + '.json')).exists())
        other.run({'action': 'apply', 'id': second, 'paths': ['file']})
        self.assertEqual((self.root / 'file').read_bytes(), b'after')

    def test_forget_preserves_pending_restore(self):
        self.write('file', b'before')
        ident = self.begin()
        self.write('file', b'after')
        self.finish(ident)
        record = self.engine.load(ident)
        record['state'] = 'partial'
        self.engine.save(record)
        with self.assertRaisesRegex(ValueError, 'Pending file restoration'):
            self.engine.run({'action': 'forget'})
        self.assertTrue((self.engine.records / (ident + '.json')).exists())

    def test_restore_preserves_git_index_and_head(self):
        subprocess.run(['git', 'init', str(self.root)], check=True, capture_output=True)
        self.write('staged.txt', b'staged baseline')
        subprocess.run(['git', '-C', str(self.root), 'add', 'staged.txt'], check=True, capture_output=True)
        index = (self.root / '.git' / 'index').read_bytes()
        head = (self.root / '.git' / 'HEAD').read_bytes()
        ident = self.begin()
        self.write('staged.txt', b'agent change')
        self.finish(ident)
        self.apply(ident, ['staged.txt'])
        self.assertEqual((self.root / '.git' / 'index').read_bytes(), index)
        self.assertEqual((self.root / '.git' / 'HEAD').read_bytes(), head)

    def test_directory_scan_failure_does_not_record_deletions(self):
        def denied(*args, **kwargs):
            kwargs['onerror'](PermissionError('denied'))
            return iter([])
        with patch.object(module.os, 'walk', side_effect=denied):
            with self.assertRaisesRegex(ValueError, 'could not be scanned'):
                self.begin()

    def test_create_modify_delete_and_undo_revert_exact_bytes(self):
        self.write('modified', b'old\r\n')
        self.write('deleted', b'\x00\xff')
        ident = self.begin()
        self.write('modified', b'new\r\n')
        (self.root / 'deleted').unlink()
        self.write('created', b'')
        manifest = self.finish(ident)
        self.assertEqual({f['path']: f['kind'] for f in manifest['files']}, {'created': 'created', 'deleted': 'deleted', 'modified': 'modified'})
        self.apply(ident, ['created', 'deleted', 'modified'])
        self.assertFalse((self.root / 'created').exists())
        self.assertEqual((self.root / 'deleted').read_bytes(), b'\x00\xff')
        self.assertEqual((self.root / 'modified').read_bytes(), b'old\r\n')
        self.apply(ident, ['created', 'deleted', 'modified'], True)
        self.assertEqual((self.root / 'created').read_bytes(), b'')
        self.assertFalse((self.root / 'deleted').exists())
        self.assertEqual((self.root / 'modified').read_bytes(), b'new\r\n')

    def test_later_changes_are_not_overwritten(self):
        self.write('a', b'old')
        ident = self.begin()
        self.write('a', b'agent')
        self.finish(ident)
        self.write('a', b'user')
        with self.assertRaises(ValueError):
            self.apply(ident, ['a'])
        self.assertEqual((self.root / 'a').read_bytes(), b'user')

    def test_recreated_deleted_file_is_conflict(self):
        self.write('a', b'old')
        ident = self.begin()
        (self.root / 'a').unlink()
        self.finish(ident)
        self.write('a', b'recreated')
        with self.assertRaises(ValueError):
            self.apply(ident, ['a'])

    def test_old_request_preserves_unrelated_later_file(self):
        ident = self.begin()
        self.write('a', b'agent')
        self.finish(ident)
        self.write('later', b'user')
        self.apply(ident, ['a'])
        self.assertEqual((self.root / 'later').read_bytes(), b'user')

    def test_overlapping_sessions_refuse_attribution(self):
        first = self.begin()
        other = module.CheckpointEngine(str(self.root), 'session-b', self.store)
        second = other.run({'action': 'begin'})['id']
        self.write('a', b'uncertain')
        self.finish(first)
        other.run({'action': 'finish', 'id': second, 'response': 'assistant:456'})
        with self.assertRaisesRegex(ValueError, 'Overlapping'):
            self.apply(first, ['a'])

    def test_other_active_session_blocks_restore(self):
        ident = self.begin()
        self.write('a', b'agent')
        self.finish(ident)
        other = module.CheckpointEngine(str(self.root), 'session-b', self.store)
        other.run({'action': 'begin'})
        with self.assertRaisesRegex(ValueError, 'active'):
            self.apply(ident, ['a'])

    def test_backup_failure_performs_no_write(self):
        ident = self.begin()
        self.write('a', b'agent')
        self.finish(ident)
        with patch.object(self.engine, 'save', side_effect=OSError('disk full')):
            with self.assertRaises(OSError):
                self.apply(ident, ['a'])
        self.assertEqual((self.root / 'a').read_bytes(), b'agent')

    def test_partial_restore_retains_undo_data_across_retry(self):
        self.write('a', b'old a')
        self.write('b', b'old b')
        ident = self.begin()
        self.write('a', b'new a')
        self.write('b', b'new b')
        self.finish(ident)
        replace = self.engine.replace
        def fail_second(relative, expected, desired):
            if relative == 'b':
                raise OSError('locked')
            return replace(relative, expected, desired)
        with patch.object(self.engine, 'replace', side_effect=fail_second):
            result = self.apply(ident, ['a', 'b'])
        self.assertEqual(result['done'], ['a'])
        self.assertEqual(result['error'], 'locked')
        self.apply(ident, ['b'])
        self.apply(ident, ['a', 'b'], True)
        self.assertEqual((self.root / 'a').read_bytes(), b'new a')
        self.assertEqual((self.root / 'b').read_bytes(), b'new b')

    def test_interrupted_write_is_reconciled_without_replay(self):
        self.write('a', b'old')
        ident = self.begin()
        self.write('a', b'new')
        self.finish(ident)
        replace = self.engine.replace
        def crash(relative, expected, desired):
            replace(relative, expected, desired)
            raise KeyboardInterrupt()
        with patch.object(self.engine, 'replace', side_effect=crash):
            with self.assertRaises(KeyboardInterrupt):
                self.apply(ident, ['a'])
        self.assertEqual(self.engine.load(ident)['state'], 'applying')
        result = self.engine.run({'action': 'recover', 'id': ident})
        self.assertEqual(result['applied'], ['a'])
        self.apply(ident, ['a'], True)
        self.assertEqual((self.root / 'a').read_bytes(), b'new')

    def test_recovery_preserves_intervening_edits(self):
        ident = self.begin()
        self.write('a', b'agent')
        self.finish(ident)
        with patch.object(self.engine, 'replace', side_effect=KeyboardInterrupt()):
            with self.assertRaises(KeyboardInterrupt):
                self.apply(ident, ['a'])
        self.write('a', b'user')
        with self.assertRaisesRegex(ValueError, 'later file changes'):
            self.engine.run({'action': 'recover', 'id': ident})
        self.assertEqual((self.root / 'a').read_bytes(), b'user')

    def test_identity_path_and_content_guards(self):
        for value in ('../outside', '/root', 'a/../b', 'C:/file', 'a\\b'):
            with self.assertRaises(ValueError):
                self.engine.path(value)
        self.write('a', b'old')
        ident = self.begin()
        self.write('a', b'new')
        self.finish(ident)
        record = self.engine.load(ident)
        (self.engine.blobs / record['before']['files']['a']['hash']).write_bytes(b'corrupt')
        with self.assertRaises(ValueError):
            self.apply(ident, ['a'])
        other = module.CheckpointEngine(str(self.root), 'different', self.store)
        with self.assertRaises(ValueError):
            other.run({'action': 'manifest', 'id': ident})

    def test_exclusions_limits_restart_and_expiration(self):
        self.write('.env', b'secret')
        ident = self.begin()
        self.write('a', b'new')
        self.write('.env', b'new secret')
        result = self.finish(ident)
        self.assertEqual([f['path'] for f in result['files']], ['a'])
        restarted = module.CheckpointEngine(str(self.root), 'session-a', self.store)
        self.assertEqual(restarted.run({'action': 'manifest', 'id': ident})['files'], result['files'])
        record = restarted.load(ident)
        record['created'] = 0
        restarted.save(record)
        result = restarted.run({'action': 'manifest', 'id': ident})
        self.assertEqual(result['state'], 'expired')
        self.assertEqual(result['files'][0]['kind'], 'created')
        with self.assertRaises(ValueError):
            self.apply(ident, ['a'])


if __name__ == '__main__':
    unittest.main()
