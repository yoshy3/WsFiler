using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Styling;
using WsFiler.Core.Commands;
using WsFiler.Core.KeyMap;
using WsFiler.App.Views;
using WsFiler.Infra.Files;
using WsFiler.Infra.Settings;
using WsFiler.Presentation.ViewModels;

namespace WsFiler.App;

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
            var settings = SettingsManager.Load();
            ApplyLanguage(settings.Language);
            ApplyTheme(settings.Theme);
            var viewModel = new MainWindowViewModel(new LocalFileSystemProvider());
            _ = viewModel.InitializeAsync(settings.LastSession?.LeftPath, settings.LastSession?.RightPath);

            desktop.MainWindow = new MainWindow(settings.KeyMap)
            {
                DataContext = viewModel
            };

            desktop.ShutdownRequested += (_, _) =>
            {
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
                    SettingsManager.Save(currentSettings);
                }
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ApplyTheme(string? theme)
    {
        RequestedThemeVariant = NormalizeTheme(theme) switch
        {
            "light" => ThemeVariant.Light,
            "dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
    }

    private static void ApplyLanguage(string? language)
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
