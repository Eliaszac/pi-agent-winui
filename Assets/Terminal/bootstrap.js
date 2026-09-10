"use strict";

// Register before loading the renderer so startup failures reach the native status area.
// Do not forward script error text, which can contain terminal content or local paths.
window.addEventListener("error", () => {
  window.chrome.webview.postMessage({ type: "renderer-error" });
}, true);
window.addEventListener("unhandledrejection", () => {
  window.chrome.webview.postMessage({ type: "renderer-error" });
});
