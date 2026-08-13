using CommunityToolkit.Mvvm.ComponentModel;

namespace OpenWindowUtility.App.ViewModels;

public abstract class PageViewModelBase : ObservableObject
{
    protected PageViewModelBase()
    {
        App.Host.Loc.LanguageChanged += (_, _) => RefreshLanguage();
    }

    protected virtual void RefreshLanguage() => OnPropertyChanged(string.Empty);
}
