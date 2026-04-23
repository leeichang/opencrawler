using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace OpenCrawler.Core.Resources;

public class LocalizationManager : INotifyPropertyChanged
{
    public static LocalizationManager Instance { get; } = new();

    private readonly ResourceManager _rm =
        new("OpenCrawler.Core.Resources.Strings", typeof(LocalizationManager).Assembly);

    private CultureInfo _culture = CultureInfo.CurrentUICulture;

    public CultureInfo Culture => _culture;

    public string this[string key] => _rm.GetString(key, _culture) ?? key;

    public string T(string key) => this[key];

    public void SetCulture(CultureInfo culture)
    {
        if (Equals(_culture, culture)) return;
        _culture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Culture)));
    }

    public void SetCultureByName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            var sys = CultureInfo.CurrentUICulture;
            name = sys.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? "zh-TW" : "en";
        }
        SetCulture(CultureInfo.GetCultureInfo(name));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
