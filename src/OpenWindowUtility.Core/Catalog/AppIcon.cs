namespace OpenWindowUtility.Core.Catalog;

public static class AppIcon
{
    private static readonly HashSet<string> TrustedHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "www.google.com",
        "google.com",
        "cdn.jsdelivr.net",
        "github.com",
        "avatars.githubusercontent.com",
        "raw.githubusercontent.com",
        "icon.horse"
    };

    public static string? UrlFor(AppEntry app)
    {
        if (TryHttps(app.Icon) is { } icon)
        {
            return icon;
        }

        if (GitHubOwnerAvatar(app.Homepage) is { } avatar)
        {
            return avatar;
        }

        return UrlFor(app.Homepage);
    }

    public static string? UrlFor(string? homepage)
    {
        if (!TryHost(homepage, out var uri))
        {
            return null;
        }

        // ponytail: Google PNG 128px; AppEntry.Icon / GitHub owner avatar when the host favicon is missing or the wrong brand
        return $"https://www.google.com/s2/favicons?sz=128&domain={uri.Host}";
    }

    public static string? GitHubOwnerAvatar(string? homepage)
    {
        if (!TryHost(homepage, out var uri) || !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var owner = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.IsNullOrWhiteSpace(owner) ? null : $"https://github.com/{owner}.png?size=128";
    }

    public static bool IsTrusted(AppEntry app, string url)
    {
        if (!TryHost(url, out var uri))
        {
            return false;
        }

        if (TrustedHosts.Contains(uri.Host))
        {
            return true;
        }

        return TryHost(app.Homepage, out var home) && SameSite(uri.Host, home.Host);
    }

    public static string? TryHttps(string? value) => TryHost(value, out _) ? value : null;

    private static bool SameSite(string iconHost, string homeHost) =>
        iconHost.Equals(homeHost, StringComparison.OrdinalIgnoreCase)
        || iconHost.EndsWith("." + homeHost, StringComparison.OrdinalIgnoreCase);

    private static bool TryHost(string? value, out Uri uri)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out uri!)
            && uri.Scheme == "https"
            && !string.IsNullOrWhiteSpace(uri.Host)
            && string.IsNullOrEmpty(uri.UserInfo))
        {
            return true;
        }

        uri = null!;
        return false;
    }
}
