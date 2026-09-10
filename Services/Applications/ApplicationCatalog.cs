using PiAgentGui.Models.Applications;

namespace PiAgentGui.Services.Applications;

public static class ApplicationCatalog
{
    public static IReadOnlyList<ApplicationDefinition> Definitions { get; } = [
        new("rider", "Rider", "rider64.exe", "rider.svg", "Rider", ApplicationKind.SolutionEditor),
        new("visualstudio", "Visual Studio", "devenv.exe", "visualstudio.svg", Kind: ApplicationKind.SolutionEditor),
        new("vscode", "VS Code", "Code.exe", "vscode.png", "Microsoft VS Code"),
        new("zed", "Zed", "zed.exe", "zed.png", "Zed"),
        new("cursor", "Cursor", "Cursor.exe", "cursor-light.svg", "cursor"),
        new("windsurf", "Windsurf", "Windsurf.exe", "windsurf-light.svg", "Windsurf"),
        new("webstorm", "WebStorm", "webstorm64.exe", "webstorm.svg", "WebStorm"),
        new("idea", "IntelliJ IDEA", "idea64.exe", "idea.svg", "IntelliJ IDEA"),
        new("pycharm", "PyCharm", "pycharm64.exe", "pycharm.svg", "PyCharm"),
        new("clion", "CLion", "clion64.exe", "clion.svg", "CLion"),
        new("goland", "GoLand", "goland64.exe", "goland.svg", "GoLand"),
        new("phpstorm", "PhpStorm", "phpstorm64.exe", "phpstorm.svg", "PhpStorm"),
        new("rubymine", "RubyMine", "rubymine64.exe", "rubymine.svg", "RubyMine"),
        new("rustrover", "RustRover", "rustrover64.exe", "rustrover.svg", "RustRover"),
        new("terminal", "Windows Terminal", "wt.exe", "terminal.svg", Kind: ApplicationKind.Terminal),
    ];
}
