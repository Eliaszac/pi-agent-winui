namespace PiAgentGui.Models.Updates;

public sealed record AppRelease(Version Version, string FileName, Uri Installer, Uri Checksum, long Size, string? Digest);
