using PiAgentGui.Models.Conversations;

namespace PiAgentGui.Services.Conversations;

/// <summary>Owns bounded background work; completion never calls a parent conversation.</summary>
public sealed class ResearchCoordinator(ResearchStore store, IResearchRunner runner) : IAsyncDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly List<ResearchTask> tasks = [];
    private readonly Dictionary<Guid, (CancellationTokenSource Cancel, Task Work)> active = [];
    private bool disposed;
    private bool initialized;
    public bool Enabled { get; private set; }
    public event Action<IReadOnlyList<ResearchTask>>? Changed;
    public event Action<string>? Failed;

    public async Task InitializeAsync()
    {
        var enabled = store.Enabled;
        tasks.AddRange((await store.LoadAsync()).Select(task => task.Status is "Queued" or "Running"
            ? task with { Status = "Interrupted", Result = "The app closed before this task finished. It was not restarted." } : task));
        await store.SaveAsync(tasks);
        Enabled = enabled;
        initialized = true;
        Changed?.Invoke(tasks.ToArray());
    }
    public async Task SetEnabledAsync(bool enabled)
    {
        await gate.WaitAsync();
        try
        {
            if (!initialized || disposed) throw new InvalidOperationException("Research storage is unavailable. Restart after resolving the storage error.");
            await store.SetEnabledAsync(enabled);
            Enabled = enabled;
            if (!enabled)
            {
                foreach (var work in active.Values) work.Cancel.Cancel();
                for (var i = 0; i < tasks.Count; i++)
                    if (tasks[i].Status == "Queued") tasks[i] = tasks[i] with { Status = "Cancelled" };
            }
            await SaveAsync();
        }
        finally { gate.Release(); }
    }
    public async Task<Guid> DispatchAsync(ResearchTask task)
    {
        if (string.IsNullOrWhiteSpace(task.Question) || task.Question.Length > 24_000 || string.IsNullOrWhiteSpace(task.Title) || task.Title.Length > 120)
            throw new ArgumentException("Supply a short title and a self-contained question of at most 24,000 characters.");
        if (string.IsNullOrWhiteSpace(task.Provider) || string.IsNullOrWhiteSpace(task.Model)) throw new ArgumentException("Select a model before dispatching research.");
        await gate.WaitAsync();
        try
        {
            if (disposed || !initialized || !Enabled) throw new InvalidOperationException("Background research is disabled.");
            if (tasks.Count(task => task.Status is "Running" or "Queued") >= 12) throw new InvalidOperationException("The research queue is full.");
            tasks.Add(task with { Status = "Queued", Result = "" });
            try { await SaveAsync(); }
            catch { tasks.RemoveAll(item => item.Id == task.Id); throw; }
            Pump();
            return task.Id;
        }
        finally { gate.Release(); }
    }
    public async Task CancelAsync(Guid id)
    {
        await gate.WaitAsync();
        try
        {
            if (active.TryGetValue(id, out var work)) work.Cancel.Cancel();
            var index = tasks.FindIndex(task => task.Id == id && task.Status == "Queued");
            if (index >= 0) tasks[index] = tasks[index] with { Status = "Cancelled" };
            await SaveAsync();
        }
        finally { gate.Release(); }
    }
    private void Pump()
    {
        if (disposed || !Enabled) return;
        foreach (var task in tasks.Where(task => task.Status == "Queued").Take(2 - active.Count).ToArray())
        {
            tasks[tasks.FindIndex(item => item.Id == task.Id)] = task with { Status = "Running" };
            var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(20));
            active.Add(task.Id, (cancellation, Task.Run(() => ExecuteAsync(task, cancellation))));
        }
        Changed?.Invoke(tasks.ToArray());
    }
    private async Task ExecuteAsync(ResearchTask task, CancellationTokenSource cancellation)
    {
        var status = "Completed";
        string result;
        try { result = await runner.RunAsync(task, cancellation.Token); }
        catch (OperationCanceledException) { status = "Cancelled"; result = "Stopped or exceeded the 20-minute limit."; }
        catch (Exception exception) { status = "Failed"; result = exception.Message; }
        await gate.WaitAsync();
        try
        {
            if (cancellation.IsCancellationRequested) status = "Cancelled";
            var index = tasks.FindIndex(item => item.Id == task.Id);
            tasks[index] = task with { Status = status, Result = result.Length > 100_000 ? result[..100_000] + "\n[Result truncated]" : result };
            active.Remove(task.Id);
            cancellation.Dispose();
            Pump();
            try { await SaveAsync(); }
            catch (Exception) { Failed?.Invoke("Couldn't save a research result. It remains available until the app closes."); }
        }
        finally { gate.Release(); }
    }
    private async Task SaveAsync() { await store.SaveAsync(tasks); Changed?.Invoke(tasks.ToArray()); }
    public async ValueTask DisposeAsync()
    {
        Task[] work;
        await gate.WaitAsync();
        try { disposed = true; foreach (var item in active.Values) item.Cancel.Cancel(); work = active.Values.Select(item => item.Work).ToArray(); }
        finally { gate.Release(); }
        await Task.WhenAll(work);
    }
}
