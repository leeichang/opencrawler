using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using OpenCrawler.App.ViewModels;

namespace OpenCrawler.App.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnLanguageChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm) vm.ChangeLanguageCommand.Execute(null);
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
