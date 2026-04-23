using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace OpenCrawler.App.Services;

public class DialogService
{
    private Window? Owner
    {
        get
        {
            var lifetime = Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            return lifetime?.MainWindow;
        }
    }

    public async Task<string?> PickFolderAsync(string title)
    {
        var owner = Owner;
        if (owner?.StorageProvider is null) return null;
        var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });
        return folders.FirstOrDefault()?.TryGetLocalPath();
    }

    public async Task<string?> PickFileAsync(string title, string filter, string ext)
    {
        var owner = Owner;
        if (owner?.StorageProvider is null) return null;
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType(filter) { Patterns = new[] { $"*.{ext}" } } }
        });
        return files.FirstOrDefault()?.TryGetLocalPath();
    }

    public Task<string?> PromptAsync(string title, string label, string defaultValue)
    {
        var owner = Owner;
        if (owner == null) return Task.FromResult<string?>(null);

        var tcs = new TaskCompletionSource<string?>();
        var textBox = new TextBox { Text = defaultValue, Width = 320 };
        var okBtn = new Button { Content = "OK", IsDefault = true, Classes = { "accent" } };
        var cancelBtn = new Button { Content = "Cancel", IsCancel = true };
        var win = new Window
        {
            Title = title,
            Width = 380,
            Height = 170,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            ShowInTaskbar = false
        };
        okBtn.Click += (_, _) => { tcs.TrySetResult(textBox.Text); win.Close(); };
        cancelBtn.Click += (_, _) => { tcs.TrySetResult(null); win.Close(); };
        win.Closed += (_, _) => tcs.TrySetResult(tcs.Task.IsCompleted ? tcs.Task.Result : null);
        win.Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(16),
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = label },
                textBox,
                new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { okBtn, cancelBtn }
                }
            }
        };
        win.ShowDialog(owner);
        return tcs.Task;
    }

    public Task<bool> ConfirmAsync(string title, string message)
    {
        var owner = Owner;
        if (owner == null) return Task.FromResult(false);
        var tcs = new TaskCompletionSource<bool>();
        var yesBtn = new Button { Content = "Yes", IsDefault = true, Classes = { "accent" } };
        var noBtn = new Button { Content = "No", IsCancel = true };
        var win = new Window
        {
            Title = title, Width = 380, Height = 160,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, CanResize = false
        };
        yesBtn.Click += (_, _) => { tcs.TrySetResult(true); win.Close(); };
        noBtn.Click += (_, _) => { tcs.TrySetResult(false); win.Close(); };
        win.Closed += (_, _) => { if (!tcs.Task.IsCompleted) tcs.TrySetResult(false); };
        win.Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(16),
            Spacing = 12,
            Children =
            {
                new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { yesBtn, noBtn }
                }
            }
        };
        win.ShowDialog(owner);
        return tcs.Task;
    }
}
