using System.Text.Json;
using OpenWindowUtility.Core.Catalog;
using OpenWindowUtility.Core.Updates;

namespace OpenWindowUtility.Core.Profiles;

public sealed class SetupProfile
{
    public string Version { get; init; } = "1";
    public List<string> SelectedAppIds { get; init; } = [];
    public List<string> SelectedTweakIds { get; init; } = [];
    public List<string> SelectedFeatureIds { get; init; } = [];
    public string? UpdatePolicy { get; init; }
    public string PackageManager { get; init; } = "auto";
}

public sealed class ProfileService
{
    public void Export(SetupProfile profile, string path)
    {
        File.WriteAllText(path, JsonSerializer.Serialize(profile, JsonOptions.Default));
    }

    public SetupProfile Import(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<SetupProfile>(json, JsonOptions.Default)
            ?? throw new InvalidOperationException("Profile file is empty.");
    }

    public static string? CanonicalUpdatePolicy(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Enum.TryParse<UpdatePolicyKind>(value, true, out var kind) ? kind.ToString() : null;
    }

    public static PackageManagerPreference ParsePackageManager(string value)
    {
        return Enum.TryParse<PackageManagerPreference>(value, true, out var preference)
            ? preference
            : PackageManagerPreference.Auto;
    }
}
