/** Bound diagnostic content and remove common credential forms before crossing RPC. */
export function mcpErrorText(value: unknown): string | undefined {
    if (typeof value !== "string" || !value.trim()) return undefined;
    return value.slice(0, 4000)
        .replace(/\x1b\[[0-?]*[ -/]*[@-~]/g, "")
        .replace(/(bearer\s+)[^\s"',;]+/gi, "$1[redacted]")
        .replace(/((?:api[_-]?key|token|authorization|password|secret)["']?\s*[:=]\s*["']?)[^\s"'&,;]+/gi, "$1[redacted]")
        .replace(/(https?:\/\/)[^\s/@]+:[^\s/@]+@/gi, "$1[redacted]@");
}
