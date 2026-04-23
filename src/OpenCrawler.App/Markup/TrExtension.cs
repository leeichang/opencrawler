using Avalonia.Data;
using Avalonia.Markup.Xaml;
using OpenCrawler.Core.Resources;

namespace OpenCrawler.App.Markup;

public class TrExtension : MarkupExtension
{
    public TrExtension() { }
    public TrExtension(string key) { Key = key; }

    public string Key { get; set; } = "";

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return new Binding
        {
            Source = LocalizationManager.Instance,
            Path = $"[{Key}]",
            Mode = BindingMode.OneWay
        };
    }
}
