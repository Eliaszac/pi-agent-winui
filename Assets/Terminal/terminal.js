/* global Terminal, FitAddon */
"use strict";

const terminal = new Terminal({ fontFamily: "Consolas, 'Cascadia Mono', monospace", fontSize: 12,
  cursorBlink: true, scrollback: 5000, screenReaderMode: true, allowProposedApi: false });
const fit = new FitAddon.FitAddon();
const host = window.chrome.webview;
terminal.loadAddon(fit);
terminal.open(document.getElementById("terminal"));
// Shell output must never be allowed to write the system clipboard via OSC 52.
terminal.parser.registerOscHandler(52, () => true);
terminal.onData(data => host.postMessage({ type: "input", data }));
terminal.onResize(size => host.postMessage({ type: "resize", columns: size.cols, rows: size.rows }));
terminal.attachCustomKeyEventHandler(event => {
  if (event.type !== "keydown" || !event.ctrlKey || !event.shiftKey) return true;
  if (event.code === "KeyC") {
    host.postMessage({ type: "copy", data: terminal.getSelection() });
    return false;
  }
  if (event.code === "KeyV") {
    host.postMessage({ type: "paste" });
    return false;
  }
  return true;
});
host.addEventListener("message", event => {
  const message = event.data;
  if (!message || typeof message.type !== "string") return;
  if (message.type === "output" && typeof message.data === "string") {
    terminal.write(message.data, () => host.postMessage({ type: "ack" }));
  } else if (message.type === "paste" && typeof message.data === "string") {
    terminal.paste(message.data);
  } else if (message.type === "theme") {
    const background = message.dark ? "#191919" : "#fafafa";
    const foreground = message.dark ? "#ededed" : "#202020";
    document.body.style.background = background;
    terminal.options.theme = { background, foreground, cursor: foreground,
      selectionBackground: message.dark ? "#ffffff35" : "#00000025" };
  } else if (message.type === "focus") {
    fit.fit();
    terminal.focus();
  } else if (message.type === "exit") {
    terminal.options.disableStdin = true;
    terminal.write("\r\n\x1b[90mShell exited. Close this tab or create another.\x1b[0m\r\n");
  }
});
new ResizeObserver(() => { if (document.body.clientWidth > 20 && document.body.clientHeight > 20) fit.fit(); }).observe(document.body);
fit.fit();
host.postMessage({ type: "ready", columns: terminal.cols, rows: terminal.rows });
terminal.focus();
