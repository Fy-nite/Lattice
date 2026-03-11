using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Avalonia.Platform.Storage;
using lattice.IR;
using lattice.TextIR;
using Lattice.Decompiler.Decompiler;

namespace Lattice.Decompiler.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    // ── Tree ─────────────────────────────────────────────────────────────────
    public ObservableCollection<TreeNodeViewModel> RootNodes { get; } = new();

    private TreeNodeViewModel? _selectedNode;
    public TreeNodeViewModel? SelectedNode
    {
        get => _selectedNode;
        set
        {
            _selectedNode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SourceText));
            OnPropertyChanged(nameof(HasSource));
        }
    }

    // ── Source pane ───────────────────────────────────────────────────────────
    public string SourceText => _selectedNode?.DecompiledSource ?? string.Empty;
    public bool HasSource => !string.IsNullOrEmpty(SourceText);

    // ── Status bar ────────────────────────────────────────────────────────────
    private string _statusText = "No file loaded.";
    public string StatusText
    {
        get => _statusText;
        private set { _statusText = value; OnPropertyChanged(); }
    }

    private string? _loadedPath;
    public string? LoadedPath
    {
        get => _loadedPath;
        private set { _loadedPath = value; OnPropertyChanged(); OnPropertyChanged(nameof(Title)); }
    }

    public string Title => LoadedPath is null
        ? "Lattice Decompiler"
        : $"Lattice Decompiler — {Path.GetFileName(LoadedPath)}";

    // ── Loading ───────────────────────────────────────────────────────────────
    public void LoadFile(string path)
    {
        try
        {
            var source = File.ReadAllText(path);
            LoadedPath = path;

            ModuleDto module;
            var trimmed = source.AsSpan().TrimStart();
            if (!trimmed.IsEmpty && (trimmed[0] == '{' || trimmed[0] == '['))
                module = JsonSerializer.Deserialize<ModuleDto>(source)
                         ?? throw new InvalidOperationException("JSON deserialisation returned null.");
            else
                module = TextIrParser.ParseModule(source);

            BuildTree(module, source);
            StatusText = $"Loaded: {path}  |  {module.types.Length} type(s)";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
    }

    private void BuildTree(ModuleDto module, string rawSource)
    {
        RootNodes.Clear();

        var moduleSource = IrPrettyPrinter.PrintModule(module);
        var moduleNode = new TreeNodeViewModel(
            $"📦  {module.name}  v{module.version}",
            "module",
            moduleSource);

        moduleNode.IsExpanded = true;

        foreach (var type in module.types)
        {
            var icon = type.kind switch
            {
                "interface" => "🔷",
                "struct"    => "🟦",
                "enum"      => "🔢",
                _           => "🟧"
            };

            var typeSource = IrPrettyPrinter.PrintType(type);
            var typeNode = new TreeNodeViewModel($"{icon}  {type.name}", type.kind, typeSource);

            // Fields
            foreach (var field in type.fields)
            {
                var fSource = IrPrettyPrinter.PrintField(field);
                typeNode.Children.Add(new TreeNodeViewModel($"🔹  {field.name} : {field.type}", "field", fSource));
            }

            // Methods / constructors
            foreach (var method in type.methods)
            {
                var mIcon = method.isStatic ? "⚡" : (method.isConstructor ? "🔨" : "🔧");
                var mLabel = method.isConstructor
                    ? $"{mIcon}  .ctor({string.Join(", ", method.parameters.Select(p => $"{p.name}: {p.type}"))})"
                    : $"{mIcon}  {method.name}({string.Join(", ", method.parameters.Select(p => $"{p.name}: {p.type}"))}) → {method.returnType}";
                var mSource = IrPrettyPrinter.PrintMethod(method, type.name);
                typeNode.Children.Add(new TreeNodeViewModel(mLabel, "method", mSource));
            }

            moduleNode.Children.Add(typeNode);
        }

        RootNodes.Add(moduleNode);
        SelectedNode = moduleNode;
    }

    // ── Open via storage API ─────────────────────────────────────────────────
    public async Task OpenFileAsync(IStorageProvider storage)
    {
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open IR Module",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Object IR")
                {
                    Patterns = new[] { "*.oir", "*.textir", "*.ir", "*.json" }
                },
                new FilePickerFileType("All files") { Patterns = new[] { "*.*" } }
            }
        });

        if (files.Count == 0) return;
        var path = files[0].TryGetLocalPath();
        if (path is not null)
            LoadFile(path);
    }

    // ── INotifyPropertyChanged ────────────────────────────────────────────────
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
