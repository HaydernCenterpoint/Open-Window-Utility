namespace OpenWindowUtility.Core.Packages;

public sealed class PackageDetails
{
    public string? Publisher { get; init; }
    public string? Version { get; init; }
    public string? ReleaseDate { get; init; }
}

public static class PackageShowParser
{
    public static PackageDetails Parse(string text)
    {
        return new PackageDetails
        {
            Publisher = FirstValue(text, "Publisher:", "Nhà phát hành:"),
            Version = FirstValue(text, "Version:", "Phiên bản:"),
            ReleaseDate = FirstValue(text, "Release Date:", "Ngày phát hành:", "Last Updated:")
        };
    }

    public static string PublisherFallback(string? wingetId)
    {
        if (string.IsNullOrWhiteSpace(wingetId))
        {
            return "—";
        }

        var dot = wingetId.IndexOf('.');
        return dot > 0 ? wingetId[..dot] : wingetId;
    }

    private static string? FirstValue(string text, params string[] labels)
    {
        using var reader = new StringReader(text);
        while (reader.ReadLine() is { } line)
        {
            var trimmed = line.Trim();
            var colon = trimmed.IndexOf(':');
            if (colon <= 0)
            {
                continue;
            }

            var key = trimmed[..colon].Trim();
            var value = trimmed[(colon + 1)..].Trim();
            if (value.Length == 0)
            {
                continue;
            }

            foreach (var label in labels)
            {
                var wanted = label.TrimEnd(':').Trim();
                if (key.Equals(wanted, StringComparison.OrdinalIgnoreCase))
                {
                    return value;
                }
            }
        }

        return null;
    }
}
