using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Diagnostics;
using Avalonia.Styling;
using System.Reflection;
using System.Threading.Tasks;
using WsFiler.Core.Commands;
using WsFiler.Core.KeyMap;
using WsFiler.App.Views;
using WsFiler.Infra.Files;
using WsFiler.Infra.Settings;
using WsFiler.Infra.Updates;
using WsFiler.Presentation.Resources;
using WsFiler.Presentation.Theming;
using WsFiler.Presentation.ViewModels;

namespace WsFiler.App;

public partial class App : Application
{
    private IDisposable? singleInstanceActivationServer;
    private string? pendingUpdateReleaseUrl;
    private string? pendingUpdateInstallerPath;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        ActualThemeVariantChanged += (_, _) => SyncUiTheme();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settings = SettingsManager.Load();
            ApplyLanguage(settings.Language);
            ApplyTheme(settings.Theme);
            var viewModel = new MainWindowViewModel(new LocalFileSystemProvider());
            viewModel.SetDirectoryHistory(DirectoryHistoryManager.Load());
            _ = viewModel.InitializeAsync(settings.LastSession?.LeftPath, settings.LastSession?.RightPath);

            var mainWindow = new MainWindow(settings.KeyMap)
            {
                DataContext = viewModel
            };

            RestoreWindowBounds(mainWindow, settings.Window);
            desktop.MainWindow = mainWindow;
            singleInstanceActivationServer = SingleInstanceCoordinator.StartActivationServer(mainWindow);
            _ = CheckForUpdatesOnStartupAsync(mainWindow, settings);

            WindowSettings? lastWindowSettings = null;
            mainWindow.PositionChanged += (_, _) => lastWindowSettings = CaptureWindowBounds(mainWindow);
            mainWindow.SizeChanged += (_, _) => lastWindowSettings = CaptureWindowBounds(mainWindow);

            desktop.ShutdownRequested += (_, _) =>
            {
                if (!string.IsNullOrWhiteSpace(pendingUpdateInstallerPath))
                {
                    StartUpdateInstaller(pendingUpdateInstallerPath);
                    pendingUpdateInstallerPath = null;
                    pendingUpdateReleaseUrl = null;
                }
                else if (!string.IsNullOrWhiteSpace(pendingUpdateReleaseUrl))
                {
                    OpenReleasePage(pendingUpdateReleaseUrl);
                    pendingUpdateReleaseUrl = null;
                }

                singleInstanceActivationServer?.Dispose();
                if (desktop.MainWindow?.DataContext is MainWindowViewModel vm)
                {
                    var currentSettings = SettingsManager.Load();
                    var paths = vm.GetCurrentPanePaths();
                    currentSettings.KeyMap ??= CreateDefaultKeyMapSettings();
                    currentSettings.Language = NormalizeLanguage(currentSettings.Language);
                    currentSettings.Theme = NormalizeTheme(currentSettings.Theme);
                    currentSettings.LastSession = new LastSessionSettings
                    {
                        LeftPath = paths.LeftPath,
                        RightPath = paths.RightPath,
                    };
                    currentSettings.Window = lastWindowSettings ?? CaptureWindowBounds(desktop.MainWindow);
                    SettingsManager.Save(currentSettings);
                    DirectoryHistoryManager.Save(vm.DirectoryHistory);
                }
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async Task CheckForUpdatesOnStartupAsync(Window owner, AppSettings startupSettings)
    {
        var updateSettings = startupSettings.UpdateCheck ?? new UpdateCheckSettings();
        if (!updateSettings.IsEnabled)
        {
            return;
        }

        GitHubReleaseInfo? release;
        try
        {
            release = await Task.Run(async () =>
            {
                var checker = new GitHubReleaseChecker();
                return await checker.CheckLatestAsync(GetCurrentVersion());
            });
        }
        catch
        {
            return;
        }

        if (release is null ||
            string.Equals(updateSettings.IgnoredVersion, release.Version, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            if (owner.PlatformImpl is null)
            {
                return;
            }

            var dialog = new UpdateAvailableDialog(release);
            var result = await dialog.ShowDialog<UpdateAvailableDialogResult?>(owner);
            if (result is null)
            {
                return;
            }

            var latestSettings = SettingsManager.Load();
            latestSettings.UpdateCheck ??= new UpdateCheckSettings();
            latestSettings.UpdateCheck.IsEnabled = !result.DisableUpdateCheck;

            switch (result.Action)
            {
                case UpdateAvailableAction.UpgradeNow:
                    latestSettings.UpdateCheck.IgnoredVersion = null;
                    await DownloadAndStartUpdateAsync(release);
                    break;
                case UpdateAvailableAction.UpgradeOnExit:
                    latestSettings.UpdateCheck.IgnoredVersion = null;
                    await DownloadUpdateForExitAsync(release);
                    break;
                case UpdateAvailableAction.Skip:
                    latestSettings.UpdateCheck.IgnoredVersion = release.Version;
                    break;
            }

            SettingsManager.Save(latestSettings);
        });
    }

    private async Task DownloadAndStartUpdateAsync(GitHubReleaseInfo release)
    {
        var installerPath = await DownloadUpdateInstallerAsync(release);
        if (string.IsNullOrWhiteSpace(installerPath))
        {
            OpenReleasePage(release.ReleaseUrl);
            return;
        }

        StartUpdateInstaller(installerPath);
    }

    private async Task DownloadUpdateForExitAsync(GitHubReleaseInfo release)
    {
        pendingUpdateReleaseUrl = release.ReleaseUrl;
        pendingUpdateInstallerPath = await DownloadUpdateInstallerAsync(release);
    }

    private static async Task<string?> DownloadUpdateInstallerAsync(GitHubReleaseInfo release)
    {
        var asset = UpdateAssetSelector.SelectForCurrentPlatform(release.Assets);
        if (asset is null)
        {
            return null;
        }

        try
        {
            var downloader = new UpdateAssetDownloader();
            return await Task.Run(async () => await downloader.DownloadAsync(asset));
        }
        catch
        {
            return null;
        }
    }

    private static string GetCurrentVersion()
    {
        var assembly = typeof(App).Assembly;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        var version = string.IsNullOrWhiteSpace(informationalVersion)
            ? assembly.GetName().Version?.ToString()
            : informationalVersion;

        return string.IsNullOrWhiteSpace(version)
            ? "0.0.0"
            : GitHubReleaseChecker.NormalizeVersionText(version);
    }

    private static void OpenReleasePage(string releaseUrl)
    {
        try
        {
            Process.Start(new ProcessStartInfo(releaseUrl)
            {
                UseShellExecute = true,
            });
        }
        catch
        {
        }
    }

    private static void StartUpdateInstaller(string installerPath)
    {
        try
        {
            Process.Start(new ProcessStartInfo(installerPath)
            {
                UseShellExecute = true,
            });
        }
        catch
        {
        }
    }

    private async void OnAboutWsFilerClick(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } owner })
        {
            return;
        }

        var dialog = new Window
        {
            Title = Strings.About_Title,
            Width = 360,
            Height = 180,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Spacing = 12,
                Margin = new Thickness(24),
                Children =
                {
                    new TextBlock
                    {
                        Text = Strings.App_Title,
                        FontSize = 22,
                        FontWeight = Avalonia.Media.FontWeight.SemiBold,
                    },
                    new TextBlock
                    {
                        Text = Strings.About_Description,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    },
                    new Button
                    {
                        Content = Strings.Dialog_Common_Ok,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                    },
                },
            },
        };

        if (dialog.Content is StackPanel panel && panel.Children[^1] is Button okButton)
        {
            okButton.Click += (_, _) => dialog.Close();
        }

        await dialog.ShowDialog(owner);
    }

    public void ApplyTheme(string? theme)
    {
        var normalized = NormalizeTheme(theme);
        RequestedThemeVariant = normalized switch
        {
            "light" => ThemeVariant.Light,
            "dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };

        SyncUiTheme();
    }

    private void SyncUiTheme()
    {
        // Derive from ActualThemeVariant — the same value FluentTheme renders with —
        // so the file-list foreground colors and cursor underline always match the
        // actual window background. A separate PlatformSettings.GetColorValues()
        // probe can disagree with ActualThemeVariant (e.g. when the app is launched
        // from a desktop .desktop entry, where platform theme detection differs),
        // which left dark text on a dark background under the NativeAOT .deb build.
        // When RequestedThemeVariant is Default, ActualThemeVariant may resolve
        // asynchronously; the ActualThemeVariantChanged handler re-syncs it then.
        UiTheme.IsLight = ActualThemeVariant == ThemeVariant.Light;
    }

    public static void ApplyLanguage(string? language)
    {
        var normalized = NormalizeLanguage(language);
        var culture = normalized == "system" ? CultureInfo.InstalledUICulture : CultureInfo.GetCultureInfo(normalized);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    private static string NormalizeLanguage(string? language)
    {
        if (string.Equals(language, "ja", StringComparison.OrdinalIgnoreCase))
        {
            return "ja";
        }

        if (string.Equals(language, "en", StringComparison.OrdinalIgnoreCase))
        {
            return "en";
        }

        return "system";
    }

    private static string NormalizeTheme(string? theme)
    {
        return string.Equals(theme, "light", StringComparison.OrdinalIgnoreCase)
            ? "light"
            : string.Equals(theme, "dark", StringComparison.OrdinalIgnoreCase)
                ? "dark"
                : "system";
    }

    private static void RestoreWindowBounds(Avalonia.Controls.Window window, WindowSettings? saved)
    {
        if (saved is null || saved.Width <= 0 || saved.Height <= 0)
        {
            return;
        }

        window.Position = new Avalonia.PixelPoint(saved.X, saved.Y);
        window.Width = saved.Width;
        window.Height = saved.Height;

        if (saved.IsMaximized)
        {
            window.WindowState = Avalonia.Controls.WindowState.Maximized;
        }
    }

    private static WindowSettings CaptureWindowBounds(Avalonia.Controls.Window window)
    {
        var isMaximized = window.WindowState == Avalonia.Controls.WindowState.Maximized;
        return new WindowSettings
        {
            X = window.Position.X,
            Y = window.Position.Y,
            Width = (int)window.Width,
            Height = (int)window.Height,
            IsMaximized = isMaximized,
        };
    }

    private static Dictionary<string, string> CreateDefaultKeyMapSettings()
    {
        var keyMap = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var binding in DefaultKeyMap.Bindings)
        {
            if (binding.CommandId is
                ApplicationCommandId.DialogConfirm or
                ApplicationCommandId.DialogCancel or
                ApplicationCommandId.FilePreview)
            {
                continue;
            }

            keyMap.TryAdd(binding.CommandId, binding.Gesture.Key);
        }

        return keyMap;
    }

}
