using Avalonia;
using System;

namespace WsFiler.App;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        using var singleInstanceMutex = SingleInstanceCoordinator.AcquireMutex(out var createdNew);
        using var singleInstanceLock = SingleInstanceCoordinator.TryAcquireLockFile();
        if (!createdNew || singleInstanceLock is null)
        {
            SingleInstanceCoordinator.RequestActivation();
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();

        if (OperatingSystem.IsLinux())
        {
            builder = builder.With(new X11PlatformOptions { WmClass = "WsFiler" });
        }

        return builder;
    }
}
