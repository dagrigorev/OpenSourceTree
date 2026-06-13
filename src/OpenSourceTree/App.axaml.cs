using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using OpenSourceTree.ViewModels;
using OpenSourceTree.Views;

namespace OpenSourceTree;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var settings = OpenSourceTree.Services.AppSettings.Instance;
        OpenSourceTree.Services.ThemeService.Apply(settings.Theme);
        OpenSourceTree.Services.Loc.Apply(settings.Language);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var vm = new MainWindowViewModel();
            desktop.MainWindow = new MainWindow
            {
                DataContext = vm
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
