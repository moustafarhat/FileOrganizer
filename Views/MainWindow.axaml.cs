using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using FileOrganizer.Services;
using FileOrganizer.ViewModels;

namespace FileOrganizer.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? Vm => DataContext as MainWindowViewModel;

    public MainWindow()
    {
        InitializeComponent();
        Opened += OnOpened;

        var dropZone = this.FindControl<Border>("FolderDropZone");
        if (dropZone is not null)
        {
            dropZone.AddHandler(DragDrop.DragOverEvent, OnDragOver);
            dropZone.AddHandler(DragDrop.DropEvent, OnDrop);
        }
    }

    private void OnOpened(object? sender, System.EventArgs e)
    {
        if (Vm is not null)
        {
            Vm.FolderPicker = PickFoldersAsync;
            Vm.ConfirmDialog = ConfirmAsync;
            Vm.ShellIntegrationAction = ShowShellIntegrationAsync;
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (Vm is null) return;
        var items = e.DataTransfer.TryGetFiles();
        if (items is null) return;

        foreach (var item in items)
        {
            var path = item.TryGetLocalPath();
            if (path is null) continue;
            if (item is IStorageFolder || System.IO.Directory.Exists(path))
            {
                Vm.AddFolder(path);
            }
        }
        e.Handled = true;
    }

    private async Task<IReadOnlyList<string>> PickFoldersAsync()
    {
        var storage = StorageProvider;
        if (storage is null) return Array.Empty<string>();

        var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose folders to organize",
            AllowMultiple = true,
        });

        return folders
            .Select(f => f.TryGetLocalPath())
            .Where(p => !string.IsNullOrEmpty(p))
            .Cast<string>()
            .ToList();
    }

    private async Task<bool> ConfirmAsync(string title, string message)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 460,
            Height = 200,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            ShowInTaskbar = false,
        };

        var result = false;
        var okBtn = new Button { Content = "Confirm", Background = new SolidColorBrush(Color.Parse("#dc2626")), Foreground = Brushes.White, MinWidth = 90 };
        var cancelBtn = new Button { Content = "Cancel", MinWidth = 90 };
        okBtn.Click += (_, _) => { result = true; dialog.Close(); };
        cancelBtn.Click += (_, _) => { result = false; dialog.Close(); };

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 14,
            Children =
            {
                new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Children = { cancelBtn, okBtn },
                },
            },
        };

        await dialog.ShowDialog(this);
        return result;
    }

    private async Task ShowShellIntegrationAsync()
    {
        var supported = ShellIntegrationService.IsSupported;
        var registered = ShellIntegrationService.IsRegistered();

        var dialog = new Window
        {
            Title = "Shell integration",
            Width = 520,
            Height = 260,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            ShowInTaskbar = false,
        };

        var description = new TextBlock
        {
            Text = supported
                ? "Add 'Organize with File Organizer' to the right-click menu when you click a folder (or a folder's empty background) in Windows Explorer. No admin needed — this writes only to your user registry hive (HKCU)."
                : "Shell integration is currently Windows-only. macOS Services and Linux file-manager integrations aren't wired up yet.",
            TextWrapping = TextWrapping.Wrap,
        };

        var statusText = new TextBlock
        {
            Margin = new Thickness(0, 4, 0, 0),
            FontWeight = FontWeight.SemiBold,
        };
        void RefreshStatus()
        {
            var nowRegistered = ShellIntegrationService.IsRegistered();
            statusText.Text = supported
                ? (nowRegistered ? "Status: Installed" : "Status: Not installed")
                : $"Detected platform: not Windows";
            statusText.Foreground = supported && nowRegistered ? new SolidColorBrush(Color.Parse("#166534")) : new SolidColorBrush(Color.Parse("#666"));
        }
        RefreshStatus();

        var installBtn = new Button { Content = "Install", MinWidth = 100, IsEnabled = supported && !registered };
        var uninstallBtn = new Button { Content = "Uninstall", MinWidth = 100, IsEnabled = supported && registered };
        var closeBtn = new Button { Content = "Close", MinWidth = 100 };

        installBtn.Click += (_, _) =>
        {
            try { ShellIntegrationService.Register(); }
            catch (Exception ex) { statusText.Text = $"Failed: {ex.Message}"; statusText.Foreground = Brushes.Red; return; }
            RefreshStatus();
            installBtn.IsEnabled = false;
            uninstallBtn.IsEnabled = true;
        };
        uninstallBtn.Click += (_, _) =>
        {
            try { ShellIntegrationService.Unregister(); }
            catch (Exception ex) { statusText.Text = $"Failed: {ex.Message}"; statusText.Foreground = Brushes.Red; return; }
            RefreshStatus();
            installBtn.IsEnabled = true;
            uninstallBtn.IsEnabled = false;
        };
        closeBtn.Click += (_, _) => dialog.Close();

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 12,
            Children =
            {
                description,
                statusText,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Children = { closeBtn, uninstallBtn, installBtn },
                },
            },
        };

        await dialog.ShowDialog(this);
    }
}
