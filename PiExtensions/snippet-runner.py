"""One explicit snippet operation on the selected POSIX target; stdin disconnect cancels owned work."""
import codecs
import json
import os
import signal
import subprocess
import sys
import tempfile
import threading

lock = threading.Lock()


def emit(kind, **values):
    with lock:
        print(json.dumps(dict(type=kind, **values)), flush=True)


def main():
    request = json.loads(sys.stdin.readline())
    os.chdir(request["path"])
    code = request["code"].encode("utf-8")
    extension = request["extension"]
    if not extension.startswith(".") or not extension[1:].isalnum():
        raise ValueError("Invalid snippet extension")
    if request["action"] == "save":
        for index in range(1, 10001):
            name = "snippet" + ("" if index == 1 else "-" + str(index)) + extension
            try:
                with open(name, "xb") as destination:
                    destination.write(code)
                emit("result", value=os.path.join(os.getcwd(), name))
                return
            except FileExistsError:
                continue
        raise ValueError("Too many snippet files already exist")
    if request["action"] != "run":
        raise ValueError("Unsupported snippet action")
    command = {"python": ["python3", "-u"], "powershell": ["pwsh", "-NoLogo", "-NoProfile", "-NonInteractive", "-File"], "bash": ["bash", "--"]}[request["language"]]
    path = None
    process = None
    try:
        with tempfile.NamedTemporaryFile(suffix=extension, delete=False) as source:
            path = source.name
            source.write(code)
        process = subprocess.Popen(command + [path], stdin=subprocess.DEVNULL, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                                   start_new_session=True, env=dict(os.environ, PYTHONIOENCODING="utf-8"))

        def stop():
            try:
                os.killpg(process.pid, signal.SIGKILL)
            except ProcessLookupError:
                pass

        def watch():
            sys.stdin.readline()
            if process.poll() is None:
                stop()

        def pump(stream, kind):
            decoder = codecs.getincrementaldecoder("utf-8")("replace")
            while True:
                chunk = stream.read1(2048)
                if not chunk:
                    tail = decoder.decode(b"", final=True)
                    if tail:
                        emit(kind, text=tail)
                    return
                emit(kind, text=decoder.decode(chunk))

        threading.Thread(target=watch, daemon=True).start()
        readers = [threading.Thread(target=pump, args=(process.stdout, "stdout")), threading.Thread(target=pump, args=(process.stderr, "stderr"))]
        for reader in readers:
            reader.start()
        result = process.wait()
        stop()
        for reader in readers:
            reader.join()
        emit("result", value=str(result))
    finally:
        if process is not None and process.poll() is None:
            os.killpg(process.pid, signal.SIGKILL)
            process.wait()
        if path:
            os.unlink(path)


if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        emit("error", text=str(error))
