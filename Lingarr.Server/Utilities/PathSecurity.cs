namespace Lingarr.Server.Utilities;

public static class PathSecurity
{
    public static bool IsPathUnderAnyRoot(string path, IEnumerable<string> roots)
    {
        return TryResolvePathUnderAnyRoot(path, roots, out _);
    }

    public static bool TryResolvePathUnderAnyRoot(string path, IEnumerable<string> roots, out string fullPath)
    {
        var resolvedPath = Path.GetFullPath(path);
        fullPath = resolvedPath;
        return roots.Any(root => IsPathUnderRoot(resolvedPath, root));
    }

    private static bool IsPathUnderRoot(string fullPath, string root)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            return false;
        }

        var fullRoot = EnsureTrailingSeparator(Path.GetFullPath(root));
        var normalizedPath = EnsureTrailingSeparator(fullPath);
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return normalizedPath.StartsWith(fullRoot, comparison);
    }

    private static string EnsureTrailingSeparator(string path)
    {
        if (path.EndsWith(Path.DirectorySeparatorChar))
        {
            return path;
        }

        return path + Path.DirectorySeparatorChar;
    }
}
