using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using WsFiler.Core.Commands;
using WsFiler.Infra.Settings;
using WsFiler.Presentation.Resources;

namespace WsFiler.App.Views;

public partial class SettingsDialog : Window
{
    private readonly AppSettings settings;
    private readonly ObservableCollection<UserCommandEntry> userCommands;
    private readonly List<SettingsEntry> entries;
    private readonly List<string> sectionOrder;
    private string activeSection = "";

    private ComboBox? themeCombo;
    private ComboBox? languageCombo;
    private CheckBox? updateCheckBox;
    private TextBox? editorTextBox;
    private ListBox? userCommandListBox;

    public SettingsDialog()
        : this(SettingsManager.Load())
    {
    }

    public SettingsDialog(AppSettings settings)
    {
        InitializeComponent();
        this.settings = settings;
        userCommands = new ObservableCollection<UserCommandEntry>(UserCommandSettingsManager.Load());

        Title = Strings.Dialog_Settings_Title;
#pragma warning disable CS0618
        SearchBox.Watermark = Strings.Dialog_Settings_SearchPlaceholder;
#pragma warning restore CS0618
        OkButton.Content = Strings.Dialog_Common_Ok;
        CancelButton.Content = Strings.Dialog_Common_Cancel;

        entries =
        [
            new(Strings.Dialog_Settings_Section_General, Strings.Dialog_Settings_Theme,
                BuildThemeEntry, [Strings.Dialog_Settings_Theme_System, Strings.Dialog_Settings_Theme_Light, Strings.Dialog_Settings_Theme_Dark]),
            new(Strings.Dialog_Settings_Section_General, Strings.Dialog_Settings_Language,
                BuildLanguageEntry, [Strings.Dialog_Settings_Language_System, Strings.Dialog_Settings_Language_English, Strings.Dialog_Settings_Language_Japanese]),
            new(Strings.Dialog_Settings_Section_General, Strings.Dialog_Settings_UpdateCheck,
                BuildUpdateCheckEntry, ["update", "release", "version", "github"]),
            new(Strings.Dialog_Settings_Section_External, Strings.Dialog_Settings_ExternalEditor,
                BuildEditorEntry, ["editor", "exe", "path"]),
            new(Strings.Dialog_Settings_Section_Bookmarks, Strings.Dialog_Bookmark_Title,
                BuildBookmarksEntry, ["bookmark"]),
            new(Strings.Dialog_Settings_Section_Keymap, Strings.Dialog_Settings_OpenKeymap,
                BuildKeymapEntry, ["keymap", "shortcut", "binding", "key"]),
            new(Strings.Dialog_Settings_Section_UserCommands, Strings.Dialog_UserCommand_Title,
                BuildUserCommandsEntry, ["command", "user", "macro", "exe", "argument"]),
        ];

        sectionOrder = entries.Select(entry => entry.Section).Distinct().ToList();

        RebuildSectionList(query: "");
        SectionList.SelectedIndex = 0;

        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close(false);
        }
    }

    private void OnSectionSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (SectionList.SelectedIndex < 0 ||
            SectionList.SelectedItem is not string title)
        {
            return;
        }

        activeSection = title;
        RenderContent(SearchBox.Text?.Trim() ?? "");
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        var query = SearchBox.Text?.Trim() ?? "";
        RebuildSectionList(query);

        if (SectionList.ItemsSource is IEnumerable<string> source && source.Any())
        {
            if (!source.Contains(activeSection))
            {
                SectionList.SelectedIndex = 0;
            }
        }

        RenderContent(query);
    }

    private void RebuildSectionList(string query)
    {
        var matchingSections = sectionOrder
            .Where(section =>
                string.IsNullOrEmpty(query) ||
                entries.Any(entry => entry.Section == section && entry.Matches(query)))
            .ToList();

        SectionList.ItemsSource = matchingSections;
    }

    private void RenderContent(string query)
    {
        ContentPanel.Children.Clear();
        themeCombo = null;
        languageCombo = null;
        updateCheckBox = null;
        editorTextBox = null;
        userCommandListBox = null;

        IEnumerable<SettingsEntry> visible;
        if (string.IsNullOrEmpty(query))
        {
            visible = entries.Where(entry => entry.Section == activeSection);
        }
        else
        {
            visible = entries.Where(entry => entry.Matches(query));
        }

        var byList = visible.ToList();
        if (byList.Count == 0)
        {
            ContentPanel.Children.Add(new TextBlock
            {
                Text = Strings.Dialog_Settings_NoMatches,
                Opacity = 0.7,
            });
            return;
        }

        string? lastSection = null;
        foreach (var entry in byList)
        {
            if (entry.Section != lastSection)
            {
                ContentPanel.Children.Add(new TextBlock
                {
                    Text = entry.Section,
                    FontSize = 11,
                    FontWeight = Avalonia.Media.FontWeight.SemiBold,
                    Opacity = 0.7,
                    Margin = new Avalonia.Thickness(0, lastSection is null ? 0 : 12, 0, 4),
                });
                lastSection = entry.Section;
            }

            ContentPanel.Children.Add(new TextBlock
            {
                Text = entry.Title,
                FontSize = 14,
                FontWeight = Avalonia.Media.FontWeight.SemiBold,
                Margin = new Avalonia.Thickness(0, 4, 0, 4),
            });
            entry.Build();
        }
    }

    private void BuildThemeEntry()
    {
        themeCombo = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = new[]
            {
                new ComboValue("system", Strings.Dialog_Settings_Theme_System),
                new ComboValue("light", Strings.Dialog_Settings_Theme_Light),
                new ComboValue("dark", Strings.Dialog_Settings_Theme_Dark),
            },
            SelectedIndex = ThemeIndex(settings.Theme),
        };
        ContentPanel.Children.Add(themeCombo);
    }

    private void BuildLanguageEntry()
    {
        languageCombo = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = new[]
            {
                new ComboValue("system", Strings.Dialog_Settings_Language_System),
                new ComboValue("en", Strings.Dialog_Settings_Language_English),
                new ComboValue("ja", Strings.Dialog_Settings_Language_Japanese),
            },
            SelectedIndex = LanguageIndex(settings.Language),
        };
        ContentPanel.Children.Add(languageCombo);

        ContentPanel.Children.Add(new TextBlock
        {
            Text = Strings.Dialog_Settings_RestartHint,
            FontSize = 11,
            Opacity = 0.7,
            Margin = new Avalonia.Thickness(0, 4, 0, 0),
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        });
    }

    private void BuildEditorEntry()
    {
        editorTextBox = new TextBox
        {
            Text = settings.ExternalEditor ?? "",
        };
        ContentPanel.Children.Add(editorTextBox);
    }

    private void BuildUpdateCheckEntry()
    {
        settings.UpdateCheck ??= new UpdateCheckSettings();
        updateCheckBox = new CheckBox
        {
            Content = Strings.Dialog_Settings_UpdateCheckEnabled,
            IsChecked = settings.UpdateCheck.IsEnabled,
        };
        ContentPanel.Children.Add(updateCheckBox);
    }

    private void BuildBookmarksEntry()
    {
        settings.DirectoryBookmarks ??= new List<string>();
        var bookmarks = new System.Collections.ObjectModel.ObservableCollection<string>(settings.DirectoryBookmarks);

        var listBox = new ListBox
        {
            ItemsSource = bookmarks,
            FontFamily = new Avalonia.Media.FontFamily("Consolas"),
            Height = 240,
        };

        var deleteButton = new Button
        {
            Content = Strings.Dialog_Bookmark_Delete,
            HorizontalAlignment = HorizontalAlignment.Left,
            MinWidth = 120,
            Margin = new Avalonia.Thickness(0, 6, 0, 0),
        };
        deleteButton.Click += (_, _) =>
        {
            if (listBox.SelectedItem is not string selected)
            {
                return;
            }

            var index = listBox.SelectedIndex;
            bookmarks.Remove(selected);
            settings.DirectoryBookmarks = bookmarks.ToList();

            if (bookmarks.Count > 0)
            {
                listBox.SelectedIndex = Math.Clamp(index, 0, bookmarks.Count - 1);
            }
        };

        ContentPanel.Children.Add(listBox);
        ContentPanel.Children.Add(deleteButton);

        if (bookmarks.Count == 0)
        {
            ContentPanel.Children.Add(new TextBlock
            {
                Text = "—",
                Opacity = 0.6,
                Margin = new Avalonia.Thickness(0, 4, 0, 0),
            });
        }
    }

    private void BuildKeymapEntry()
    {
        var openButton = new Button
        {
            Content = Strings.Dialog_Settings_OpenKeymap,
            HorizontalAlignment = HorizontalAlignment.Left,
            MinWidth = 220,
        };
        openButton.Click += async (_, _) => await OpenKeymapDialogAsync();
        ContentPanel.Children.Add(openButton);
    }

    private async Task OpenKeymapDialogAsync()
    {
        var userCommandIds = userCommands
            .Where(command => !string.IsNullOrWhiteSpace(command.Name))
            .Select(command => UserCommandDefinition.ToCommandId(command.Name!.Trim()));
        var dialog = new KeyMapDialog(settings.KeyMap ?? new Dictionary<string, string>(), userCommandIds);
        var result = await dialog.ShowDialog<Dictionary<string, string>?>(this);
        if (result is not null)
        {
            settings.KeyMap = result;
        }
    }

    private void BuildUserCommandsEntry()
    {
        userCommandListBox = new ListBox
        {
            ItemsSource = BuildUserCommandRows(),
            Height = 220,
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Avalonia.Thickness(0, 6, 0, 0),
            Children =
            {
                MakeButton(Strings.Dialog_UserCommand_Add, async () => await AddUserCommandAsync()),
                MakeButton(Strings.Dialog_UserCommand_Edit, async () => await EditUserCommandAsync()),
                MakeButton(Strings.Dialog_UserCommand_Delete, DeleteSelectedUserCommand),
            },
        };

        ContentPanel.Children.Add(userCommandListBox);
        ContentPanel.Children.Add(buttons);
    }

    private Button MakeButton(string content, Action action)
    {
        var button = new Button
        {
            Content = content,
            MinWidth = 92,
        };
        button.Click += (_, _) => action();
        return button;
    }

    private IReadOnlyList<UserCommandRow> BuildUserCommandRows()
    {
        return userCommands
            .Select(command => new UserCommandRow(command))
            .OrderBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void RefreshUserCommandRows(string? selectName = null)
    {
        if (userCommandListBox is null)
        {
            return;
        }

        var rows = BuildUserCommandRows();
        userCommandListBox.ItemsSource = rows;
        if (!string.IsNullOrWhiteSpace(selectName))
        {
            userCommandListBox.SelectedItem = rows.FirstOrDefault(row =>
                string.Equals(row.Name, selectName, StringComparison.OrdinalIgnoreCase));
        }
    }

    private async Task AddUserCommandAsync()
    {
        var dialog = new UserCommandDialog(null, userCommands.Select(command => command.Name ?? ""));
        var result = await dialog.ShowDialog<UserCommandEntry?>(this);
        if (result is null)
        {
            return;
        }

        userCommands.Add(result);
        RefreshUserCommandRows(result.Name);
    }

    private async Task EditUserCommandAsync()
    {
        if (userCommandListBox?.SelectedItem is not UserCommandRow row)
        {
            return;
        }

        var command = row.Command;
        var oldName = command.Name?.Trim() ?? "";
        var dialog = new UserCommandDialog(command, userCommands.Select(item => item.Name ?? ""));
        var result = await dialog.ShowDialog<UserCommandEntry?>(this);
        if (result is null)
        {
            return;
        }

        command.Name = result.Name;
        command.ExecutablePath = result.ExecutablePath;
        command.Arguments = result.Arguments;
        command.WorkingDirectoryMode = result.WorkingDirectoryMode;

        var newName = result.Name?.Trim() ?? "";
        if (!string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase))
        {
            MoveUserCommandKeyBinding(oldName, newName);
        }

        RefreshUserCommandRows(newName);
    }

    private void DeleteSelectedUserCommand()
    {
        if (userCommandListBox?.SelectedItem is not UserCommandRow row)
        {
            return;
        }

        userCommands.Remove(row.Command);
        RemoveUserCommandKeyBinding(row.Name);
        RefreshUserCommandRows();
    }

    private void MoveUserCommandKeyBinding(string oldName, string newName)
    {
        if (string.IsNullOrWhiteSpace(oldName) || string.IsNullOrWhiteSpace(newName))
        {
            return;
        }

        settings.KeyMap ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var oldCommandId = UserCommandDefinition.ToCommandId(oldName);
        var newCommandId = UserCommandDefinition.ToCommandId(newName);
        if (RemoveKeyMapEntry(oldCommandId, out var gesture))
        {
            settings.KeyMap[newCommandId] = gesture;
        }
    }

    private void RemoveUserCommandKeyBinding(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        RemoveKeyMapEntry(UserCommandDefinition.ToCommandId(name), out _);
    }

    private bool RemoveKeyMapEntry(string commandId, out string gesture)
    {
        gesture = "";
        if (settings.KeyMap is null)
        {
            return false;
        }

        var key = settings.KeyMap.Keys.FirstOrDefault(existing =>
            string.Equals(existing, commandId, StringComparison.OrdinalIgnoreCase));
        if (key is null)
        {
            return false;
        }

        gesture = settings.KeyMap[key];
        settings.KeyMap.Remove(key);
        return true;
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        ApplyEdits();
        SettingsManager.Save(settings);
        UserCommandSettingsManager.Save(userCommands);
        Close(true);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private void ApplyEdits()
    {
        if (themeCombo?.SelectedItem is ComboValue theme)
        {
            settings.Theme = theme.Key;
        }
        if (languageCombo?.SelectedItem is ComboValue language)
        {
            settings.Language = language.Key;
        }
        if (editorTextBox is not null)
        {
            settings.ExternalEditor = string.IsNullOrWhiteSpace(editorTextBox.Text)
                ? null
                : editorTextBox.Text;
        }
        if (updateCheckBox is not null)
        {
            settings.UpdateCheck ??= new UpdateCheckSettings();
            settings.UpdateCheck.IsEnabled = updateCheckBox.IsChecked == true;
        }
    }

    private static int ThemeIndex(string? value) => value switch
    {
        "light" => 1,
        "dark" => 2,
        _ => 0,
    };

    private static int LanguageIndex(string? value) => value switch
    {
        "en" => 1,
        "ja" => 2,
        _ => 0,
    };

    private sealed record ComboValue(string Key, string Display)
    {
        public override string ToString() => Display;
    }

    private sealed record UserCommandRow(UserCommandEntry Command)
    {
        public string Name => Command.Name?.Trim() ?? "";

        public override string ToString()
        {
            var executable = Command.ExecutablePath?.Trim() ?? "";
            var arguments = Command.Arguments ?? "";
            var workingDirectory = string.Equals(
                Command.WorkingDirectoryMode,
                UserCommandEntry.WorkingDirectoryExecutable,
                StringComparison.OrdinalIgnoreCase)
                ? Strings.Dialog_UserCommand_WorkingDirectoryExecutable
                : Strings.Dialog_UserCommand_WorkingDirectoryCurrent;
            var commandLine = string.IsNullOrWhiteSpace(arguments)
                ? executable
                : $"{executable} {arguments}";
            return $"{Name}  |  {commandLine}  |  {workingDirectory}";
        }
    }

    private sealed class SettingsEntry
    {
        public string Section { get; }
        public string Title { get; }
        private readonly Action build;
        private readonly string[] keywords;

        public SettingsEntry(string section, string title, Action build, string[] keywords)
        {
            Section = section;
            Title = title;
            this.build = build;
            this.keywords = keywords;
        }

        public void Build() => build();

        public bool Matches(string query) =>
            Section.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            keywords.Any(keyword => keyword.Contains(query, StringComparison.OrdinalIgnoreCase));
    }
}
