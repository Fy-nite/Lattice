using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Lattice.Decompiler.ViewModels;

namespace Lattice.Decompiler;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            desktop.MainWindow = window;

            // Allow passing a file path as a CLI argument: lattice-decompiler mymodule.oir
            var args = desktop.Args ?? [];
            if (args.Length > 0 && File.Exists(args[0]))
            {
                var vm = (MainViewModel)window.DataContext!;
                vm.LoadFile(args[0]);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}