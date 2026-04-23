using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using OpenCrawler.App.ViewModels;

namespace OpenCrawler.App.Views;

public partial class NotesTabView : UserControl
{
    public NotesTabView()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    protected override void OnDataContextChanged(System.EventArgs e)
    {
        base.OnDataContextChanged(e);
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        if (DataContext is NotesTabViewModel vm)
            _ = vm.SaveAllAsync();
        base.OnUnloaded(e);
    }
}
