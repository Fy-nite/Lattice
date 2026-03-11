using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Lattice.Decompiler.ViewModels;

namespace Lattice.Decompiler;

public partial class MainWindow : Window
{
    private MainViewModel Vm => (MainViewModel)DataContext!;

    public MainWindow()
    {
        InitializeComponent();
        // Drag-and-drop: open a dropped .oir / .json file
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        DragDrop.SetAllowDrop(this, true);

        // Keyboard shortcut: Ctrl+O
        KeyDown += OnKeyDown;
    }

    private async void OnOpenClicked(object? sender, RoutedEventArgs e)
        => await Vm.OpenFileAsync(StorageProvider);

    private void OnReloadClicked(object? sender, RoutedEventArgs e)
    {
        if (Vm.LoadedPath is { } path)
            Vm.LoadFile(path);
    }

    private async void OnCopySourceClicked(object? sender, RoutedEventArgs e)
    {
        if (Clipboard is null) return;
        await Clipboard.SetTextAsync(Vm.SourceText);
    }

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.O && e.KeyModifiers == KeyModifiers.Meta ||
            e.Key == Key.O && e.KeyModifiers == KeyModifiers.Control)
            await Vm.OpenFileAsync(StorageProvider);
    }

    private void OnDragOver(object? sender, DragEventArgs e)
        => e.DragEffects = e.Data.Contains(DataFormats.Files) ? DragDropEffects.Copy : DragDropEffects.None;

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (!e.Data.Contains(DataFormats.Files)) return;
        var files = e.Data.GetFiles();
        if (files is null) return;
        foreach (var f in files)
        {
            var path = (f as IStorageFile)?.Path.LocalPath ?? f.TryGetLocalPath();
            if (path is not null) { Vm.LoadFile(path); break; }
        }
    }
}