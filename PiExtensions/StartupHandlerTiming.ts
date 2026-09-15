type Handler = (...args: unknown[]) => unknown;
type Runner = { extensions?: { path?: string; handlers: Map<string, Handler[]> }[] };
type Prototype = { emit: (this: Runner, event: { type: string }, ...args: unknown[]) => Promise<unknown> };

/** Temporary startup diagnostics; delegates dispatch and error handling to Pi. */
export class StartupHandlerTiming {
    static install(prototype: Prototype, record: (label: string, milliseconds: number) => void): void {
        const marker = Symbol.for("pi-gui.startup-handler-timing");
        if (Reflect.get(prototype, marker)) return;
        const original = prototype.emit;
        if (typeof original !== "function") return;
        const report = (label: string, start: number): void => {
            try { record(label, Math.round(performance.now() - start)); } catch { /* Diagnostics must not affect startup. */ }
        };
        prototype.emit = async function (event, ...args) {
            if (event.type !== "session_start") return original.call(this, event, ...args);
            const start = performance.now();
            const restore: (() => void)[] = [];
            try {
                if (Array.isArray(this.extensions)) {
                    this.extensions.forEach((extension, index) => {
                        const handlers = extension.handlers?.get(event.type);
                        if (!Array.isArray(handlers)) return;
                        const name = (extension.path ?? "extension").split(/[\\/]/).pop() ?? "extension";
                        const label = /^[A-Za-z0-9_.-]{1,80}$/.test(name) ? name : "extension";
                        handlers.forEach((handler, handlerIndex) => {
                            const wrapped: Handler = async function (this: unknown, ...values) {
                                const began = performance.now();
                                try { return await handler.apply(this, values); }
                                finally { report(`session_start.${index}.${label}.${handlerIndex}`, began); }
                            };
                            handlers[handlerIndex] = wrapped;
                            restore.push(() => { if (handlers[handlerIndex] === wrapped) handlers[handlerIndex] = handler; });
                        });
                    });
                }
                return await original.call(this, event, ...args);
            } finally {
                restore.forEach(action => action());
                report("session_start.TOTAL", start);
            }
        };
        Reflect.set(prototype, marker, true);
    }
}
