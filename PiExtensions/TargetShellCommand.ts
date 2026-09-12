import { TargetConnection } from "./TargetConnection.ts";

/** A per-command Linux process group whose lifetime follows the transport's control pipe. */
export class TargetShellCommand {
    static wrap(command: string): string {
        return `command -v setsid >/dev/null || exit 127
exec 3<&0
setsid bash -lc ${TargetConnection.quote(command)} < /dev/null &
job=$!
( IFS= read -r control <&3; kill -TERM -- "-$job" 2>/dev/null ) &
watcher=$!
cleanup() { kill -TERM -- "-$job" 2>/dev/null; kill "$watcher" 2>/dev/null; }
trap cleanup EXIT
trap 'exit 130' HUP INT TERM
wait "$job"
status=$?
exit "$status"`;
    }
}
