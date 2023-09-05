using Avalonia;
using Avalonia.Markup.Xaml;
using Avalonia.Controls.ApplicationLifetimes;

using simple_lan_file_transfer.Views;
using simple_lan_file_transfer.Services;
using simple_lan_file_transfer.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace simple_lan_file_transfer;

public partial class App : Application
{
    public static IServiceProvider? Services { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();

        var mainViewModel = new MainViewModel();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = mainViewModel
            };

            services.AddSingleton<IExposeStorageProviderService>(_ => new ExposeStorageProviderService(desktop.MainWindow));
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView
            {
                DataContext = mainViewModel
            };

            services.AddSingleton<IExposeStorageProviderService>(_ => new ExposeStorageProviderService(singleViewPlatform.MainView));
        }

        Services = services.BuildServiceProvider();
        mainViewModel.GetAndStoreStorageProviderService();

        base.OnFrameworkInitializationCompleted();
    }
}