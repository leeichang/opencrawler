using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace OpenCrawler.App.Views;

public partial class FirstRunWindow : Window
{
    public FirstRunWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
