"""Unit tests for target helper protocol; no real interpreters or remote hosts are started."""
import importlib.util
import io
import json
import os
from pathlib import Path
import tempfile
import threading
import unittest
from unittest.mock import patch, MagicMock

spec = importlib.util.spec_from_file_location("snippet_runner", Path(__file__).parents[2] / "PiExtensions" / "snippet-runner.py")
runner = importlib.util.module_from_spec(spec)
spec.loader.exec_module(runner)


class SnippetRunnerTests(unittest.TestCase):
    def invoke(self, request):
        output = io.StringIO()
        previous = os.getcwd()
        try:
            with patch.object(runner.sys, "stdin", io.StringIO(json.dumps(request) + "\nstop\n")), patch.object(runner.sys, "stdout", output):
                runner.main()
        finally:
            os.chdir(previous)
        return [json.loads(line) for line in output.getvalue().splitlines()]

    def test_save_preserves_bytes_and_skips_collisions(self):
        previous = os.getcwd()
        try:
            with tempfile.TemporaryDirectory() as folder:
                Path(folder, "snippet.py").write_text("original", encoding="utf-8")
                request = dict(action="save", path=folder, extension=".py", code="print('héllo')\r\n")
                result = self.invoke(request)
                saved = Path(result[0]["value"])
                self.assertEqual(saved.name, "snippet-2.py")
                self.assertEqual(saved.read_bytes(), request["code"].encode("utf-8"))
                self.assertEqual(Path(folder, "snippet.py").read_text(), "original")
                os.chdir(previous)
        finally:
            os.chdir(previous)

    def test_run_uses_isolated_process_and_cleans_temporary_source(self):
        previous = os.getcwd()
        try:
            with tempfile.TemporaryDirectory() as folder:
                process = MagicMock()
                process.pid = 123
                process.stdout = io.BytesIO("héllo".encode())
                process.stderr = io.BytesIO(b"problem")
                process.wait.return_value = 7
                process.poll.return_value = 7
                with patch.object(runner.subprocess, "Popen", return_value=process) as launch, patch.object(runner.os, "killpg", create=True) as kill, patch.object(runner.signal, "SIGKILL", 9, create=True):
                    result = self.invoke(dict(action="run", language="python", path=folder, extension=".py", code="pass"))
                args, options = launch.call_args
                self.assertEqual(args[0][:2], ["python3", "-u"])
                self.assertTrue(options["start_new_session"])
                self.assertEqual(options["stdin"], runner.subprocess.DEVNULL)
                self.assertFalse(Path(args[0][-1]).exists())
                self.assertIn(dict(type="stdout", text="héllo"), result)
                self.assertIn(dict(type="stderr", text="problem"), result)
                self.assertEqual(result[-1], dict(type="result", value="7"))
                kill.assert_called()
                os.chdir(previous)
        finally:
            os.chdir(previous)

    def test_extension_cannot_escape_project_root(self):
        with self.assertRaises(ValueError):
            self.invoke(dict(action="save", path=os.getcwd(), extension="/../bad", code="test"))

    def test_stop_input_kills_the_owned_process_group(self):
        stopped = threading.Event()
        with tempfile.TemporaryDirectory() as folder:
            process = MagicMock()
            process.pid = 456
            process.stdout = io.BytesIO()
            process.stderr = io.BytesIO()
            process.poll.side_effect = lambda: -9 if stopped.is_set() else None

            def wait():
                self.assertTrue(stopped.wait(2), "Stop was not forwarded")
                return -9

            process.wait.side_effect = wait
            with patch.object(runner.subprocess, "Popen", return_value=process), patch.object(runner.os, "killpg", side_effect=lambda *_: stopped.set(), create=True) as kill, patch.object(runner.signal, "SIGKILL", 9, create=True):
                result = self.invoke(dict(action="run", language="bash", path=folder, extension=".sh", code="sleep 100"))
            kill.assert_any_call(456, 9)
            self.assertEqual(result[-1], dict(type="result", value="-9"))


if __name__ == "__main__":
    unittest.main()
