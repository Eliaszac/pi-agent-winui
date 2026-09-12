using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.Conversations;

public sealed partial class ConversationViewModel
{
    private readonly TimeProvider processingClock;
    private ITimer? processingTimer;
    private long? processingStarted;
    private int processingTickPending;
    private string ProcessingLabel => (compacting || previewCompacting ? "Compacting context… " : "Processing… ") +
        ProcessingDuration.Format(processingStarted is { } started ? processingClock.GetElapsedTime(started) : TimeSpan.Zero);

    private void SynchronizeProcessingTime()
    {
        if (disposed) return;
        if (running || compacting || previewCompacting)
        {
            if (processingStarted is null)
            {
                processingStarted = processingClock.GetTimestamp();
                processingTimer = processingClock.CreateTimer(_ =>
                {
                    if (Interlocked.Exchange(ref processingTickPending, 1) != 0) return;
                    dispatcher.Post(() =>
                    {
                        Interlocked.Exchange(ref processingTickPending, 0);
                        if (!disposed && processingStarted is not null) presentation.UpdateProcessingLabel(ProcessingLabel);
                    });
                }, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
            }
            presentation.UpdateProcessingLabel(ProcessingLabel);
        }
        else
        {
            processingTimer?.Dispose();
            processingTimer = null;
            processingStarted = null;
        }
    }
}
