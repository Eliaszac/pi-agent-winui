type BranchEntry = { id: string; type: string; message?: { role: string; timestamp?: number } };

/** Associates a checkpoint only with a new assistant on the branch where capture began. */
export class CheckpointResponseScope {
    private readonly existing: Set<string>;
    private readonly timestamps: Set<number | undefined>;
    private readonly boundary: string | undefined;
    private settledResponse: string | undefined;

    constructor(branch: readonly BranchEntry[]) {
        this.existing = new Set(branch.map(entry => entry.id));
        this.timestamps = new Set(branch.filter(entry => entry.message?.role === "assistant").map(entry => entry.message?.timestamp));
        this.boundary = branch.at(-1)?.id;
    }

    settle(branch: readonly BranchEntry[]): string {
        if (this.settledResponse !== undefined) return this.settledResponse;
        const boundaryIndex = this.boundary === undefined ? -1 : branch.findIndex(entry => entry.id === this.boundary);
        const response = this.boundary !== undefined && boundaryIndex < 0 ? undefined
            : branch.slice(boundaryIndex + 1).reverse().find(entry => entry.type === "message"
                && entry.message?.role === "assistant" && !this.existing.has(entry.id)
                && typeof entry.message.timestamp === "number" && Number.isFinite(entry.message.timestamp)
                && !this.timestamps.has(entry.message.timestamp));
        this.settledResponse = response ? `assistant:${response.message!.timestamp}` : "";
        return this.settledResponse;
    }
}
