using Microsoft.VisualBasic.FileIO;

namespace PiAgentGui.Services.Files;

public static class RecycleBin
{
    public static void Delete(string path, bool directory)
    {
        if (directory) FileSystem.DeleteDirectory(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin, UICancelOption.ThrowException);
        else FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin, UICancelOption.ThrowException);
    }
}
