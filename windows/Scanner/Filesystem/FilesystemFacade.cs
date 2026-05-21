using System.Runtime.InteropServices;
using System.Text;

namespace AIExposureScanner.Scanner.Filesystem;

public interface IFilesystemFacade
{
    string HomeDirectory { get; }
    string ApplicationDataDirectory { get; }
    string LocalApplicationDataDirectory { get; }

    bool FileExists(string path);
    bool DirectoryExists(string path);
    string ReadTextFile(string path, int maxBytes = 10 * 1024 * 1024);
    IReadOnlyList<string> ListDirectoryNames(string path);
    IReadOnlyList<string> ListFilesRecursively(string path, int maxDepth);
    bool IsWorldReadableFile(string path);
}

public sealed class LocalFilesystem : IFilesystemFacade
{
    public LocalFilesystem(
        string? homeDirectory = null,
        string? applicationDataDirectory = null,
        string? localApplicationDataDirectory = null
    )
    {
        HomeDirectory = homeDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        ApplicationDataDirectory = applicationDataDirectory ??
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(ApplicationDataDirectory))
        {
            ApplicationDataDirectory = Path.Combine(HomeDirectory, "AppData", "Roaming");
        }
        LocalApplicationDataDirectory = localApplicationDataDirectory ??
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(LocalApplicationDataDirectory))
        {
            LocalApplicationDataDirectory = Path.Combine(HomeDirectory, "AppData", "Local");
        }
    }

    public string HomeDirectory { get; }
    public string ApplicationDataDirectory { get; }
    public string LocalApplicationDataDirectory { get; }

    public bool FileExists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public string ReadTextFile(string path, int maxBytes = 10 * 1024 * 1024)
    {
        var info = new FileInfo(path);
        if (info.Length > maxBytes)
        {
            throw new IOException($"File is larger than the {maxBytes} byte scan limit: {path}");
        }
        return File.ReadAllText(path, Encoding.UTF8);
    }

    public IReadOnlyList<string> ListDirectoryNames(string path) =>
        Directory.Exists(path)
            ? Directory.EnumerateFileSystemEntries(path).Select(Path.GetFileName).Where(name => name is not null).Cast<string>().ToArray()
            : [];

    public IReadOnlyList<string> ListFilesRecursively(string path, int maxDepth)
    {
        if (!Directory.Exists(path))
        {
            return [];
        }
        var results = new List<string>();
        ListFilesRecursively(path, 0, maxDepth, results);
        return results;
    }

    public bool IsWorldReadableFile(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return WindowsAclScanner.HasBroadReadAccess(path);
        }

        try
        {
            var mode = File.GetUnixFileMode(path);
            return (mode & UnixFileMode.OtherRead) != 0;
        }
        catch
        {
            return false;
        }
    }

    private static void ListFilesRecursively(string path, int currentDepth, int maxDepth, List<string> results)
    {
        if (currentDepth > maxDepth)
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(path))
        {
            results.Add(file);
        }

        foreach (var directory in Directory.EnumerateDirectories(path))
        {
            // Skip junctions and symlinks to avoid infinite loops
            var attrs = File.GetAttributes(directory);
            if ((attrs & FileAttributes.ReparsePoint) != 0) continue;
            ListFilesRecursively(directory, currentDepth + 1, maxDepth, results);
        }
    }
}

internal static class WindowsAclScanner
{
    private const uint DaclSecurityInformation = 0x00000004;
    private const uint FileGenericRead = 0x00120089;
    private const uint GenericRead = 0x80000000;
    private const byte AccessAllowedAceType = 0x00;
    private const int SecurityMaxSidSize = 68;

    public static bool HasBroadReadAccess(string path)
    {
        var result = GetNamedSecurityInfo(
            path,
            SE_OBJECT_TYPE.SE_FILE_OBJECT,
            DaclSecurityInformation,
            out _,
            out _,
            out var dacl,
            out _,
            out var securityDescriptor
        );

        if (result != 0 || dacl == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            var acl = Marshal.PtrToStructure<ACL>(dacl);
            for (uint index = 0; index < acl.AceCount; index++)
            {
                if (!GetAce(dacl, index, out var ace) || ace == IntPtr.Zero)
                {
                    continue;
                }

                var header = Marshal.PtrToStructure<ACE_HEADER>(ace);
                if (header.AceType != AccessAllowedAceType)
                {
                    continue;
                }

                var accessMask = (uint)Marshal.ReadInt32(ace, 4);
                if (!GrantsRead(accessMask))
                {
                    continue;
                }

                var sid = IntPtr.Add(ace, 8);
                if (IsBroadPrincipal(sid))
                {
                    return true;
                }
            }
        }
        finally
        {
            if (securityDescriptor != IntPtr.Zero)
            {
                LocalFree(securityDescriptor);
            }
        }

        return false;
    }

    private static bool GrantsRead(uint accessMask) =>
        (accessMask & GenericRead) != 0 || (accessMask & FileGenericRead) == FileGenericRead;

    private static bool IsBroadPrincipal(IntPtr sid) =>
        EqualWellKnownSid(sid, WELL_KNOWN_SID_TYPE.WinWorldSid) ||
        EqualWellKnownSid(sid, WELL_KNOWN_SID_TYPE.WinBuiltinUsersSid);

    private static bool EqualWellKnownSid(IntPtr sid, WELL_KNOWN_SID_TYPE type)
    {
        var wellKnownSid = Marshal.AllocHGlobal(SecurityMaxSidSize);
        try
        {
            var size = (uint)SecurityMaxSidSize;
            return CreateWellKnownSid(type, IntPtr.Zero, wellKnownSid, ref size) && EqualSid(sid, wellKnownSid);
        }
        finally
        {
            Marshal.FreeHGlobal(wellKnownSid);
        }
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetNamedSecurityInfo(
        string pObjectName,
        SE_OBJECT_TYPE objectType,
        uint securityInfo,
        out IntPtr ppsidOwner,
        out IntPtr ppsidGroup,
        out IntPtr ppDacl,
        out IntPtr ppSacl,
        out IntPtr ppSecurityDescriptor
    );

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetAce(IntPtr acl, uint aceIndex, out IntPtr ace);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool EqualSid(IntPtr sid1, IntPtr sid2);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool CreateWellKnownSid(
        WELL_KNOWN_SID_TYPE wellKnownSidType,
        IntPtr domainSid,
        IntPtr sid,
        ref uint cbSid
    );

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LocalFree(IntPtr hMem);

    private enum SE_OBJECT_TYPE
    {
        SE_FILE_OBJECT = 1
    }

    private enum WELL_KNOWN_SID_TYPE
    {
        WinWorldSid = 1,
        WinBuiltinUsersSid = 27
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct ACL
    {
        public readonly byte AclRevision;
        public readonly byte Sbz1;
        public readonly ushort AclSize;
        public readonly ushort AceCount;
        public readonly ushort Sbz2;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct ACE_HEADER
    {
        public readonly byte AceType;
        public readonly byte AceFlags;
        public readonly ushort AceSize;
    }
}
