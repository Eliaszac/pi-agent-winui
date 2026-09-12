using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace PiAgentGui.Services.Projects;

/// <summary>Stores only this application's SSH secrets in Windows Credential Manager.</summary>
public sealed class SshCredentialStore
{
    private static string Resource(Guid id) => "PiAgentGui.Ssh." + id.ToString("N");
    public void Save(Guid id, string expectedPrompt, string secret)
    {
        if (id == Guid.Empty || secret.Length == 0 || secret.IndexOfAny(['\r', '\n', '\0']) >= 0 || Encoding.UTF8.GetByteCount(secret) > 900)
            throw new ArgumentException("Enter an SSH password or passphrase of up to 900 UTF-8 bytes without line breaks.");
        var pointer = Marshal.StringToCoTaskMemUni(secret);
        try
        {
            var credential = new Credential { Type = 1, TargetName = Resource(id), UserName = expectedPrompt, CredentialBlobSize = checked((uint)Encoding.Unicode.GetByteCount(secret)), CredentialBlob = pointer, Persist = 2 };
            if (!CredWrite(ref credential, 0)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Couldn't save the SSH credential in Windows Credential Manager.");
        }
        finally { Marshal.ZeroFreeCoTaskMemUnicode(pointer); }
    }
    public (string Prompt, string Secret)? Read(Guid id)
    {
        if (!CredRead(Resource(id), 1, 0, out var pointer))
        {
            if (Marshal.GetLastWin32Error() == 1168) return null;
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Couldn't read the saved SSH credential.");
        }
        try
        {
            var value = Marshal.PtrToStructure<Credential>(pointer);
            if (value.CredentialBlobSize > 4096 || value.CredentialBlobSize % 2 != 0) return null;
            return (value.UserName ?? "", Marshal.PtrToStringUni(value.CredentialBlob, (int)value.CredentialBlobSize / 2) ?? "");
        }
        finally { CredFree(pointer); }
    }
    public void Delete(Guid id)
    {
        if (!CredDelete(Resource(id), 1, 0) && Marshal.GetLastWin32Error() != 1168)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Couldn't remove the SSH credential.");
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Credential
    {
        public uint Flags, Type;
        public string TargetName;
        public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public nint CredentialBlob;
        public uint Persist, AttributeCount;
        public nint Attributes;
        public string? TargetAlias;
        public string? UserName;
    }
    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CredWrite(ref Credential credential, uint flags);
    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CredRead(string target, uint type, uint flags, out nint credential);
    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CredDelete(string target, uint type, uint flags);
    [DllImport("advapi32.dll")] private static extern void CredFree(nint pointer);
}
