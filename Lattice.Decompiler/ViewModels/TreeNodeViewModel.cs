using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Lattice.Decompiler.ViewModels;

/// <summary>
/// A node in the IR tree (module → type → method/field).
/// </summary>
public sealed class TreeNodeViewModel : INotifyPropertyChanged
{
    public string Label { get; }
    public string Icon { get; }
    public string DecompiledSource { get; }
    public ObservableCollection<TreeNodeViewModel> Children { get; } = new();

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set { _isExpanded = value; OnPropertyChanged(); }
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    public TreeNodeViewModel(string label, string icon, string decompiledSource = "")
    {
        Label = label;
        Icon = icon;
        DecompiledSource = decompiledSource;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
