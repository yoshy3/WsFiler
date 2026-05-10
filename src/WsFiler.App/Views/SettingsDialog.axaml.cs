using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WsFiler.Infra.Settings;
using WsFiler.Presentation.Resources;

namespace WsFiler.App.Views;

public partial class SettingsDialog : Window
{
    private readonly AppSettings settings;
    private readonly List<SettingsEntry> entries;
    private readonly List<string> sectionOrder;
    private string activeSection = "";

    private ComboBox? themeCombo;
    private ComboBox? languageCombo;
    private TextBox? editorTextBox;

    public SettingsDialog()
        : this(SettingsManager.Load())
    {
    }

    public SettingsDialog(AppSettings settings)
    {
        InitializeComponent();
        this.settings = settings;

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
            new(Strings.Dialog_Settings_Section_External, Strings.Dialog_Settings_ExternalEditor,
                BuildEditorEntry, ["editor", "exe", "path"]),
            new(Strings.Dialog_Settings_Section_Bookmarks, Strings.Dialog_Bookmark_Title,
                BuildBookmarksEntry, ["bookmark"]),
            new(Strings.Dialog_Settings_Section_Keymap, Strings.Dialog_Settings_OpenKeymap,
                BuildKeymapEntry, ["keymap", "shortcut", "binding", "key"]),
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
        editorTextBox = null;

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
        var dialog = new KeyMapDialog(settings.KeyMap ?? new Dictionary<string, string>());
        var result = await dialog.ShowDialog<Dictionary<string, string>?>(this);
        if (result is not null)
        {
            settings.KeyMap = result;
        }
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        ApplyEdits();
        SettingsManager.Save(settings);
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
