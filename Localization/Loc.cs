using System.ComponentModel;

namespace BKKleaner.Localization;

/// <summary>
/// XAML-bindable localization source. Usage:
/// Text="{Binding [dashboard.title], Source={x:Static loc:Loc.Instance}}"
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    private static ILocalizationService? _service;

    public static Loc Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    private Loc() { }

    public static void Initialize(ILocalizationService service)
    {
        _service = service;
        service.LanguageChanged += (_, _) =>
            Instance.PropertyChanged?.Invoke(Instance, new PropertyChangedEventArgs("Item[]"));
    }

    public string this[string key] => _service?[key] ?? key;
}
