using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using PiAgentGui.Models.Conversations;
using PiAgentGui.Models.Projects;
using PiAgentGui.Services.Conversations;
using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.Conversations;

public sealed class ArtifactPanelViewModel(ArtifactStore store, ExecutionTarget target) : ObservableObject
{
    public ArtifactStore Store { get; } = store;
    public Action<ArtifactItemViewModel>? AttachToMessage { get; set; }
    public async Task<ArtifactItemViewModel> UploadAsync(string path)
    {
        var name = ArtifactStore.ValidateName(Path.GetFileName(path));
        var bytes = await Task.Run(async () =>
        {
            await using var stream = File.OpenRead(path);
            return await ArtifactSourceReader.ReadBoundedAsync(stream, lifetime.Token);
        }, lifetime.Token);
        var record = await Store.SaveAsync(name, bytes, ArtifactFileTypes.ImageMime(bytes) ?? ArtifactFileTypes.Mime(name), "Uploaded file", sourceKey: "upload:" + Guid.NewGuid().ToString("N"), token: lifetime.Token, uniqueName: true, shared: false);
        Get(record.Id).Update(record);
        await RefreshAsync();
        return Get(record.Id);
    }
    public async Task ShareAsync(IReadOnlyList<Guid> ids, bool refresh = true)
    {
        await Store.ShareAsync(ids, lifetime.Token);
        if (refresh) await RefreshAsync();
    }
    public async Task DeleteAsync(Guid id)
    {
        await Store.DeleteAsync(id);
        await RefreshAsync();
        try { await new ArtifactTargetStorage(Store, target).DeleteAsync(id, lifetime.Token); }
        catch (Exception) { Error = "Artifact deleted locally. Remote working-copy cleanup is pending until the target is available and artifacts are accessed again, or the conversation is deleted."; }
    }
    private readonly CancellationTokenSource lifetime = new();
    public void Cancel() => lifetime.Cancel();
    private readonly Dictionary<Guid, ArtifactItemViewModel> known = [];
    private readonly HashSet<string> screenshots = [];
    private int refreshGeneration;
    public ObservableCollection<ArtifactItemViewModel> Items { get; } = [];
    private string error = "";
    public string Error { get => error; set { if (SetProperty(ref error, value)) OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => Error.Length > 0;
    public bool IsEmpty => Items.Count == 0;
    public ArtifactItemViewModel Get(Guid id)
    {
        if (!known.TryGetValue(id, out var item)) known[id] = item = new(new(id, "Artifact", "", 0, default, default, Guid.Empty, ""), this);
        return item;
    }
    public async Task RefreshAsync()
    {
        var generation = ++refreshGeneration;
        try
        {
            var records = await Store.ListAsync(lifetime.Token);
            if (generation != refreshGeneration || lifetime.IsCancellationRequested) return;
            foreach (var record in records) Get(record.Id).Update(record);
            var next = records.Where(record => !record.Deleted).Select(record => Get(record.Id)).ToArray();
            if (!Items.SequenceEqual(next)) { Items.Clear(); foreach (var item in next) Items.Add(item); }
            OnPropertyChanged(nameof(IsEmpty));
            Error = "";
        }
        catch (OperationCanceledException) { }
        catch (Exception exception) { Error = "Could not load artifacts. " + exception.Message; }
    }
    public async Task<JsonObject> HandleAsync(JsonElement request)
    {
        var action = PiJson.Text(request, "action");
        if (action == "list")
        {
            var records = await Store.ListAsync(lifetime.Token);
            return new() { ["artifacts"] = JsonSerializer.SerializeToNode(records.Where(item => !item.Deleted && item.Shared).Select(item => new { id = item.Id, name = item.Name, size = item.Size, origin = item.Origin })) };
        }
        if (action is "read" or "path")
        {
            if (!Guid.TryParse(PiJson.Text(request, "artifactId"), out var requestedId)) throw new IOException("Invalid artifact ID.");
            var record = (await Store.ListAsync(lifetime.Token)).FirstOrDefault(item => item.Id == requestedId && !item.Deleted && item.Shared) ?? throw new IOException("Artifact not found in this conversation's sent files.");
            if (action == "path") return new() { ["path"] = await Task.Run(() => new ArtifactTargetStorage(Store, target).MaterializeAsync(record, lifetime.Token), lifetime.Token), ["name"] = record.Name, ["target"] = target.Label };
            var path = await Store.GetPathAsync(record.Id);
            var offset = 0;
            var limit = 16000;
            if (request.TryGetProperty("offset", out var offsetValue) && (offsetValue.ValueKind != JsonValueKind.Number || !offsetValue.TryGetInt32(out offset))) throw new IOException("Offset must be an integer.");
            if (request.TryGetProperty("limit", out var limitValue) && (limitValue.ValueKind != JsonValueKind.Number || !limitValue.TryGetInt32(out limit))) throw new IOException("Limit must be an integer.");
            return await Task.Run(() => new ArtifactContentReader().ReadAsync(path, offset, limit, lifetime.Token), lifetime.Token);
        }
        if (action != "save") throw new IOException("Unknown artifact operation.");
        var name = ArtifactStore.ValidateName(PiJson.Text(request, "name"));
        var source = PiJson.Text(request, "sourcePath");
        var hasContent = request.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String;
        if (hasContent == (source.Length > 0)) throw new IOException("Provide either text content or a source file path.");
        lifetime.Token.ThrowIfCancellationRequested();
        if (hasContent && Encoding.UTF8.GetByteCount(content.GetString()!) > ArtifactStore.MaximumBytes) throw new IOException("Artifacts can be up to 32 MB.");
        var bytes = hasContent ? Encoding.UTF8.GetBytes(content.GetString()!) : await new ArtifactSourceReader().ReadAsync(target, source, lifetime.Token);
        var idText = PiJson.Text(request, "artifactId");
        Guid? id = idText.Length == 0 ? null : Guid.TryParse(idText, out var parsed) ? parsed : throw new IOException("Invalid artifact ID.");
        if ((await Store.ListAsync(lifetime.Token)).Any(item => !item.Deleted && !item.Shared && (item.Id == id || item.Name.Equals(name, StringComparison.OrdinalIgnoreCase))))
            throw new IOException("This name or ID belongs to an unsent upload. Choose another artifact name.");
        var requestId = PiJson.Text(request, "requestId");
        var saved = await Store.SaveAsync(name, bytes, ArtifactFileTypes.Mime(name), target.Label, id, requestId.Length == 0 ? null : requestId, lifetime.Token, requireSharedExisting: true);
        await RefreshAsync();
        return new() { ["artifactId"] = saved.Id.ToString("D"), ["name"] = saved.Name, ["size"] = saved.Size };
    }
    public async Task ObserveImagesAsync(IReadOnlyList<ChatEntry> entries)
    {
        var changed = false;
        var failure = "";
        IReadOnlyList<ArtifactRecord> existing;
        try { existing = await Store.ListAsync(lifetime.Token); }
        catch (OperationCanceledException) { return; }
        catch (Exception exception) { Error = "Could not load screenshots. " + exception.Message; return; }
        var imported = existing.Where(record => record.SourceKey is not null).Select(record => record.SourceKey!).ToHashSet();
        foreach (var entry in entries)
        for (var index = 0; index < (entry.Images?.Count ?? 0); index++)
        {
            var key = "screenshot:" + entry.Id + ":" + index;
            if (!screenshots.Add(key)) continue;
            if (imported.Contains(key)) continue;
            try
            {
                var image = entry.Images![index];
                var suffix = image.MimeType == "image/jpeg" ? ".jpg" : image.MimeType == "image/webp" ? ".webp" : ".png";
                var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..10].ToLowerInvariant();
                if (image.Data.Length > (ArtifactStore.MaximumBytes + 2L) / 3 * 4) throw new IOException("Screenshot exceeds the 32 MB artifact limit.");
                var bytes = await Task.Run(() => Convert.FromBase64String(image.Data), lifetime.Token);
                await Store.SaveAsync("Screenshot-" + hash + suffix, bytes, image.MimeType, "Conversation screenshot", sourceKey: key, token: lifetime.Token, uniqueName: true);
                changed = true;
            }
            catch (OperationCanceledException) { return; }
            catch (Exception exception) { screenshots.Remove(key); failure = "Could not add screenshot to artifacts. " + exception.Message; }
        }
        if (changed || existing.Count > 0) await RefreshAsync();
        if (failure.Length > 0) Error = failure;
    }
}
