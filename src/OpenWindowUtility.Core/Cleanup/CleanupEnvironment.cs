namespace OpenWindowUtility.Core.Cleanup;

public sealed class CleanupEnvironment
{
    public required string Windows { get; init; }
    public required string ProgramData { get; init; }
    public required string SystemDrive { get; init; }
    public required IReadOnlyList<string> UserProfiles { get; init; }

    public static CleanupEnvironment Live()
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var systemDrive = Path.GetPathRoot(windows) ?? @"C:\";
        var usersRoot = Path.Combine(systemDrive, "Users");
        var profiles = new List<string>();
        if (Directory.Exists(usersRoot))
        {
            foreach (var dir in Directory.EnumerateDirectories(usersRoot))
            {
                var name = Path.GetFileName(dir);
                if (name is "Public" or "Default" or "Default User" or "All Users")
                {
                    continue;
                }

                if (IsReparsePoint(dir))
                {
                    continue;
                }

                profiles.Add(dir);
            }
        }

        return new CleanupEnvironment
        {
            Windows = windows,
            ProgramData = programData,
            SystemDrive = systemDrive,
            UserProfiles = profiles
        };
    }

    public static bool IsReparsePoint(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch (Exception)
        {
            return true;
        }
    }
}
