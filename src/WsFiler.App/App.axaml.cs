using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using WsFiler.Core.Commands;
using WsFiler.Core.KeyMap;
using WsFiler.App.Views;
using WsFiler.Infra.Files;
using WsFiler.Presentation.ViewModels;

namespace WsFiler.App;

public partial class App : Application
{
    private const string SettingsFileName = "settings.json";

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settings = LoadSettings();
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
                    var currentSettings = LoadSettings();
                    var paths = vm.GetCurrentPanePaths();
                    currentSettings.KeyMap ??= CreateDefaultKeyMapSettings();
                    currentSettings.LastSession = new LastSessionSettings
                    {
                        LeftPath = paths.LeftPath,
                        RightPath = paths.RightPath,
                    };
                    SaveSettings(currentSettings);
                }
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static string GetSettingsPath()
    {
        return Path.Combine(AppContext.BaseDirectory, SettingsFileName);
    }

    private static AppSettings LoadSettings()
    {
        try
        {
            var path = GetSettingsPath();
            if (!File.Exists(path))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    private static void SaveSettings(AppSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(GetSettingsPath(), json);
        }
        catch
        {
        }
    }

    private static Dictionary<string, string> CreateDefaultKeyMapSettings()
    {
        var keyMap = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var binding in DefaultKeyMap.Bindings)
        {
            if (binding.CommandId is ApplicationCommandId.DialogConfirm or ApplicationCommandId.DialogCancel)
            {
                continue;
            }

            keyMap.TryAdd(binding.CommandId, binding.Gesture.Key);
        }

        return keyMap;
    }

    private static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private sealed class AppSettings
    {
        [JsonPropertyName("lastSession")]
        public LastSessionSettings? LastSession { get; set; }

        [JsonPropertyName("keyMap")]
        public Dictionary<string, string>? KeyMap { get; set; }
    }

    private sealed class LastSessionSettings
    {
        [JsonPropertyName("leftPath")]
        public string? LeftPath { get; set; }

        [JsonPropertyName("rightPath")]
        public string? RightPath { get; set; }
    }
}
