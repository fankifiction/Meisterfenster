using System.IO;

namespace Wochenplanung.Presentation.Services;

public static class AppLaunchLocation
{
    public static string LocalAppFolder =>
        Path.Combine(AppPaths.Root, "app");

    public static bool IsNetworkPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var full = path.Trim();
        if (full.StartsWith(@"\\", StringComparison.Ordinal) || full.StartsWith("//", StringComparison.Ordinal))
            return true;

        try
        {
            var root = Path.GetPathRoot(full);
            if (string.IsNullOrEmpty(root) || root.StartsWith(@"\\", StringComparison.Ordinal))
                return !string.IsNullOrEmpty(root);
            return new DriveInfo(root).DriveType == DriveType.Network;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsLocalAppCopy(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            var full = Path.GetFullPath(path);
            var local = Path.GetFullPath(LocalAppFolder);
            return full.StartsWith(local.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(Path.GetDirectoryName(full), local, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static string CopyBundle(string sourceExe, string destDir)
    {
        var sourceDir = Path.GetDirectoryName(Path.GetFullPath(sourceExe))
                        ?? throw new InvalidOperationException("Quellordner fehlt.");
        Directory.CreateDirectory(destDir);

        foreach (var sourceFile in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(destDir, Path.GetFileName(sourceFile));
            if (IsSameFile(sourceFile, destFile))
                continue;
            File.Copy(sourceFile, destFile, overwrite: true);
        }

        foreach (var sourceSub in Directory.GetDirectories(sourceDir))
        {
            var name = Path.GetFileName(sourceSub);
            if (string.Equals(name, "logs", StringComparison.OrdinalIgnoreCase))
                continue;
            CopyDirectory(sourceSub, Path.Combine(destDir, name));
        }

        return Path.Combine(destDir, Path.GetFileName(sourceExe));
    }

    public static string CopyToLocalApp(string sourceExe) => CopyBundle(sourceExe, LocalAppFolder);

    public static bool IsSameFile(string sourcePath, string destPath)
    {
        if (!File.Exists(destPath))
            return false;
        var source = new FileInfo(sourcePath);
        var dest = new FileInfo(destPath);
        return source.Length == dest.Length && source.LastWriteTimeUtc == dest.LastWriteTimeUtc;
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        foreach (var sourceFile in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(destDir, Path.GetFileName(sourceFile));
            if (IsSameFile(sourceFile, destFile))
                continue;
            File.Copy(sourceFile, destFile, overwrite: true);
        }

        foreach (var sourceSub in Directory.GetDirectories(sourceDir))
            CopyDirectory(sourceSub, Path.Combine(destDir, Path.GetFileName(sourceSub)));
    }
}
