# Terminal renderer

Bundled npm distributions: `@xterm/xterm` 6.0.0 and `@xterm/addon-fit` 0.11.0, downloaded from unpkg.com. Their MIT licenses are included alongside the assets. No CDN access is used at runtime.

`terminal.js` connects xterm.js to the native ConPTY session through WebView2 messages. Output is terminal text, never HTML or script. One output chunk is acknowledged at a time to bound buffering. Remote navigation, network requests, popups and OSC 52 clipboard writes are disabled. Ctrl+Shift+C/V provides user-initiated copy/paste.
