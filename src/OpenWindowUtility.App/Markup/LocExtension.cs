using System.Windows.Data;
using System.Windows.Markup;
using OpenWindowUtility.App.Services;

namespace OpenWindowUtility.App.Markup;

public sealed class LocExtension : MarkupExtension
{
    public LocExtension()
    {
    }

    public LocExtension(string key)
    {
        Key = key;
    }

    public string Key { get; set; } = "";

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        LocalizationService? source = null;
        if (App.Host is not null)
        {
            source = App.Host.Loc;
        }

        var binding = new Binding($"[{Key}]")
        {
            Source = source,
            Mode = BindingMode.OneWay
        };
        return binding.ProvideValue(serviceProvider);
    }
}
