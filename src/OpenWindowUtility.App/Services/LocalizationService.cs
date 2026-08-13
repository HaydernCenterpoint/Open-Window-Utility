using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using OpenWindowUtility.Core.Catalog;
using OpenWindowUtility.Core.Settings;

namespace OpenWindowUtility.App.Services;

public sealed class LocalizationService : INotifyPropertyChanged
{
    private readonly CatalogI18n _catalog;
    private readonly Dictionary<string, string> _en;
    private readonly Dictionary<string, string> _vi;

    public LocalizationService(CatalogI18n catalog)
    {
        _catalog = catalog;
        _en = UiCopy.English;
        _vi = UiCopy.Vietnamese;
        Language = ResolveInitial();
    }

    public string Language { get; private set; }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? LanguageChanged;

    public string this[string key] => GetUi(key);

    public string Catalog(string key) => _catalog.Get(Language, key);

    public void ApplySettings(AppSettings settings)
    {
        SetLanguage(settings.Language);
    }

    public void SetLanguage(string language)
    {
        var normalized = Normalize(language);
        if (string.Equals(Language, normalized, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Language = normalized;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(Binding.IndexerName));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public string GetUi(string key)
    {
        var table = Language.StartsWith("vi", StringComparison.OrdinalIgnoreCase) ? _vi : _en;
        if (table.TryGetValue(key, out var value))
        {
            return value;
        }

        return _en.TryGetValue(key, out var fallback) ? fallback : key;
    }

    private static string ResolveInitial()
    {
        var culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return culture.Equals("vi", StringComparison.OrdinalIgnoreCase) ? "vi" : "en";
    }

    private static string Normalize(string language)
    {
        if (language.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveInitial();
        }

        return language.StartsWith("vi", StringComparison.OrdinalIgnoreCase) ? "vi" : "en";
    }
}
