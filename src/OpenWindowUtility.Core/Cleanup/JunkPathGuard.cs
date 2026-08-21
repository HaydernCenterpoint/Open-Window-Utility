namespace OpenWindowUtility.Core.Cleanup;

public static class JunkPathGuard
{
    public static bool IsUnderRoot(string path, string root)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(root))
        {
            return false;
        }

        string fullPath;
        string fullRoot;
        try
        {
            fullPath = Path.GetFullPath(path);
            fullRoot = Path.GetFullPath(root);
        }
        catch (Exception)
        {
            return false;
        }

        if (fullPath.Equals(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var prefix = fullRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                     + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsStrictlyUnderRoot(string path, string root) =>
        IsUnderRoot(path, root) && !PathsEqual(path, root);

    public static bool CanDelete(string path, JunkKind kind, CleanupEnvironment env)
    {
        if (kind == JunkKind.RecycleBin || string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        string full;
        try
        {
            full = Path.GetFullPath(path);
        }
        catch (Exception)
        {
            return false;
        }

        if (LooksProtected(full, env) || LooksKeptUserData(full, env))
        {
            return false;
        }

        foreach (var root in JunkCatalog.RootsFor(kind, env))
        {
            if (!IsUnderRoot(full, root.Path))
            {
                continue;
            }

            if (kind == JunkKind.WindowsOld && PathsEqual(full, root.Path))
            {
                return true;
            }

            if (IsStrictlyUnderRoot(full, root.Path))
            {
                return true;
            }

            if (!root.Recurse && PathsEqual(full, root.Path) && System.IO.File.Exists(full))
            {
                return true;
            }
        }

        return false;
    }

    private static bool LooksProtected(string full, CleanupEnvironment env)
    {
        string[] protectedRoots =
        [
            Path.Combine(env.Windows, "System32"),
            Path.Combine(env.Windows, "SysWOW64"),
            Path.Combine(env.Windows, "WinSxS"),
            Path.Combine(env.SystemDrive, "Program Files"),
            Path.Combine(env.SystemDrive, "Program Files (x86)")
        ];
        foreach (var protectedRoot in protectedRoots)
        {
            if (IsUnderRoot(full, protectedRoot))
            {
                return true;
            }
        }

        return false;
    }

    private static bool LooksKeptUserData(string full, CleanupEnvironment env)
    {
        string[] kept =
        [
            @".ollama\models",
            @".cursor\projects",
            @".vscode\extensions",
            @".lmstudio\models"
        ];
        foreach (var profile in env.UserProfiles)
        {
            foreach (var relative in kept)
            {
                if (IsUnderRoot(full, Path.Combine(profile, relative)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool PathsEqual(string left, string right)
    {
        try
        {
            return Path.GetFullPath(left).Equals(Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return false;
        }
    }
}
