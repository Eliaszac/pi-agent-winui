using PiAgentGui.ViewModels.Projects;

namespace PiAgentGui.Controls;

public sealed class SidebarTemplateSelector : DataTemplateSelector
{
    public DataTemplate ProjectTemplate { get; set; } = null!;
    public DataTemplate ConversationTemplate { get; set; } = null!;
    public DataTemplate SettledTemplate { get; set; } = null!;
    public DataTemplate EmptyTemplate { get; set; } = null!;
    protected override DataTemplate SelectTemplateCore(object item) => item switch
    {
        ProjectItemViewModel => ProjectTemplate,
        SidebarGroupRow { IsEmpty: true } => EmptyTemplate,
        SidebarGroupRow => SettledTemplate,
        _ => ConversationTemplate
    };
    protected override DataTemplate SelectTemplateCore(object item, DependencyObject container) => SelectTemplateCore(item);
}
