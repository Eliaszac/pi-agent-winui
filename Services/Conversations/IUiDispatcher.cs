namespace PiAgentGui.Services.Conversations;

/// <summary>Schedules presentation updates on the owning UI thread.</summary>
public interface IUiDispatcher
{
    bool Post(Action action);
    bool PostBackground(Action action) => Post(action);
}
