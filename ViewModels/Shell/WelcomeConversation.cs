using PiAgentGui.Utilities;
using PiAgentGui.ViewModels.Projects;

namespace PiAgentGui.ViewModels.Shell;

public sealed record WelcomeConversation(string ProjectName, ConversationItemViewModel Conversation)
{
    public string Title => Conversation.Title;
    public RelayCommand OpenCommand => Conversation.SelectCommand;
}
