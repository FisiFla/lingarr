namespace Lingarr.Server.Utilities;

public static class PathSecurity
{
    public static bool IsPathUnderAnyRoot(string path, IEnumerable<string> roots)
    {
        var fullPath = Path.GetFullPath(path);
        return roots.Any(root => IsPathUnderRoot(fullPath, root));
    }

    private static bool IsPathUnderRoot(string fullPath, string root)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            return false;
        }

        var fullRoot = EnsureTrailingSeparator(Path.GetFullPath(root));
        var normalizedPath = EnsureTrailingSeparator(Path.GetFullPath(fullPath));
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return normalizedPath.StartsWith(fullRoot, comparison);
    }

    private static string EnsureTrailingSeparator(string path)
    {
        if (path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar))
        {
            return path;
        }

        return path + Path.DirectorySeparatorChar;
    }
}
