using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.Home;

public sealed record HomeConversation(Guid Id, Guid ProjectId, string Title, string ProjectName, string LastOpened, RelayCommand OpenCommand);
