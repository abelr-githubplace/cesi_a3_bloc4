using System.Management;
using System.Runtime.Versioning;

namespace Sanitize
{
    [SupportedOSPlatform("windows")]
    internal static class UNCResolver
    {
        internal static string? ResolveToUNC(string path)
        {
            var absolute = Path.GetFullPath(path);
            if (absolute.StartsWith(@"\\")) return absolute;

            string? rootPath = ResolveToRootUNC(absolute);
            if (rootPath == null) return null;
            if (absolute.StartsWith(rootPath)) return absolute;
            else return absolute.Replace(GetDriveLetter(absolute), rootPath);
        }

        private static string? ResolveToRootUNC(string path)
        {
            string driveletter = GetDriveLetter(path);

            using ManagementObject mo = new();
            mo.Path = new ManagementPath(string.Format("Win32_LogicalDisk='{0}'", driveletter));
            DriveType driveType = (DriveType)((uint)mo["DriveType"]);
            string? networkRoot = Convert.ToString(mo["ProviderName"]);

            if (driveType == DriveType.Network) return networkRoot;
            else return driveletter + Path.DirectorySeparatorChar;
        }

        private static string GetDriveLetter(string path)
            => Directory.GetDirectoryRoot(path).Replace(Path.DirectorySeparatorChar.ToString(), "");
    }

    public static class PathSanitizer
    {
        [SupportedOSPlatform("windows")]
        private static string? ToUNC(string path) => UNCResolver.ResolveToUNC(path);

        public static string? Sanitize(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            if (!OperatingSystem.IsWindows()) return path;
            string? root;
            try { root = ToUNC(path); } catch { return null; }

            if (root == null) return null;
            if (!new Uri(root).IsUnc) return root;
            return root;
        }
    }
}
