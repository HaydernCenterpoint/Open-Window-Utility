using System.Text.Json;

namespace OpenWindowUtility.Core.Catalog;

public sealed class CatalogI18n
{
    private readonly IReadOnlyDictionary<string, string> _en;
    private readonly IReadOnlyDictionary<string, string> _vi;

    public CatalogI18n(IReadOnlyDictionary<string, string> en, IReadOnlyDictionary<string, string> vi)
    {
        _en = en;
        _vi = vi;
    }

    public IEnumerable<string> EnglishKeys => _en.Keys;

    public string Get(string language, string key)
    {
        var table = language.StartsWith("vi", StringComparison.OrdinalIgnoreCase) ? _vi : _en;
        if (table.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (_en.TryGetValue(key, out var fallback) && !string.IsNullOrWhiteSpace(fallback))
        {
            return fallback;
        }

        return key;
    }

    public static CatalogI18n LoadFromJson(string enJson, string viJson)
    {
        var en = JsonSerializer.Deserialize<Dictionary<string, string>>(enJson, JsonOptions.Default)
            ?? throw new InvalidOperationException("en.json is empty.");
        var vi = JsonSerializer.Deserialize<Dictionary<string, string>>(viJson, JsonOptions.Default)
            ?? throw new InvalidOperationException("vi.json is empty.");
        return new CatalogI18n(en, vi);
    }
}
