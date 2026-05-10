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
    private readonly List<SettingsSection> sections;
    private SettingsSection? activeSection;

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

        sections =
        [
            new(Strings.Dialog_Settings_Section_General, BuildGeneralPanel,
                [Strings.Dialog_Settings_Theme, Strings.Dialog_Settings_Language]),
            new(Strings.Dialog_Settings_Section_External, BuildExternalPanel,
                [Strings.Dialog_Settings_ExternalEditor]),
            new(Strings.Dialog_Settings_Section_Bookmarks, BuildBookmarksPanel,
                [Strings.Dialog_Bookmark_Title]),
            new(Strings.Dialog_Settings_Section_Keymap, BuildKeymapPanel,
                [Strings.Dialog_Keymap_Title, Strings.Dialog_Settings_OpenKeymap]),
        ];

        SectionList.ItemsSource = sections.Select(section => section.Title).ToList();
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
        if (SectionList.SelectedIndex < 0)
        {
            return;
        }

        if (SectionList.ItemsSource is IEnumerable<string> source)
        {
            var titles = source.ToList();
            if (SectionList.SelectedIndex >= titles.Count)
            {
                return;
            }

            var title = titles[SectionList.SelectedIndex];
            activeSection = sections.FirstOrDefault(section => section.Title == title);
            RenderActiveSection();
        }
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        var query = SearchBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(query))
        {
            SectionList.ItemsSource = sections.Select(section => section.Title).ToList();
            if (SectionList.SelectedIndex < 0 && sections.Count > 0)
            {
                SectionList.SelectedIndex = 0;
            }
            return;
        }

        var filtered = sections
            .Where(section => section.Matches(query))
            .ToList();

        SectionList.ItemsSource = filtered.Select(section => section.Title).ToList();

        if (filtered.Count > 0)
        {
            activeSection = filtered[0];
            SectionList.SelectedIndex = 0;
            RenderActiveSection();
        }
        else
        {
            ContentPanel.Children.Clear();
            ContentPanel.Children.Add(new TextBlock
            {
                Text = Strings.Dialog_Settings_NoMatches,
                Opacity = 0.7,
            });
        }
    }

    private void RenderActiveSection()
    {
        ContentPanel.Children.Clear();
        themeCombo = null;
        languageCombo = null;
        editorTextBox = null;
        activeSection?.Build();
    }

    private void BuildGeneralPanel()
    {
        AddHeader(Strings.Dialog_Settings_Section_General);

        ContentPanel.Children.Add(new TextBlock { Text = Strings.Dialog_Settings_Theme });
        themeCombo = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = new[]
            {
                new ComboValue("system", Strings.Dialog_Settings_Theme_System),
                new ComboValue("light", Strings.Dialog_Settings_Theme_Light),
                new ComboValue("dark", Strings.Dialog_Settings_Theme_Dark),
            },
            DisplayMemberBinding = new Avalonia.Data.Binding(nameof(ComboValue.Display)),
            SelectedIndex = ThemeIndex(settings.Theme),
        };
        ContentPanel.Children.Add(themeCombo);

        ContentPanel.Children.Add(new TextBlock
        {
            Text = Strings.Dialog_Settings_Language,
            Margin = new Avalonia.Thickness(0, 8, 0, 0),
        });
        languageCombo = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = new[]
            {
                new ComboValue("system", Strings.Dialog_Settings_Language_System),
                new ComboValue("en", Strings.Dialog_Settings_Language_English),
                new ComboValue("ja", Strings.Dialog_Settings_Language_Japanese),
            },
            DisplayMemberBinding = new Avalonia.Data.Binding(nameof(ComboValue.Display)),
            SelectedIndex = LanguageIndex(settings.Language),
        };
        ContentPanel.Children.Add(languageCombo);

        ContentPanel.Children.Add(new TextBlock
        {
            Text = Strings.Dialog_Settings_RestartHint,
            FontSize = 11,
            Opacity = 0.7,
            Margin = new Avalonia.Thickness(0, 12, 0, 0),
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        });
    }

    private void BuildExternalPanel()
    {
        AddHeader(Strings.Dialog_Settings_Section_External);

        ContentPanel.Children.Add(new TextBlock { Text = Strings.Dialog_Settings_ExternalEditor });
        editorTextBox = new TextBox
        {
            Text = settings.ExternalEditor ?? "",
        };
        ContentPanel.Children.Add(editorTextBox);
    }

    private void BuildBookmarksPanel()
    {
        AddHeader(Strings.Dialog_Bookmark_Title);

        var bookmarks = settings.DirectoryBookmarks ?? new List<string>();
        if (bookmarks.Count == 0)
        {
            ContentPanel.Children.Add(new TextBlock { Text = "—", Opacity = 0.6 });
            return;
        }

        ContentPanel.Children.Add(new ListBox
        {
            ItemsSource = bookmarks.ToList(),
            FontFamily = new Avalonia.Media.FontFamily("Consolas"),
            Height = 280,
        });
    }

    private void BuildKeymapPanel()
    {
        AddHeader(Strings.Dialog_Settings_Section_Keymap);

        var openButton = new Button
        {
            Content = Strings.Dialog_Settings_OpenKeymap,
            HorizontalAlignment = HorizontalAlignment.Left,
            MinWidth = 220,
        };
        openButton.Click += async (_, _) => await OpenKeymapDialogAsync();
        ContentPanel.Children.Add(openButton);
    }

    private void AddHeader(string text)
    {
        ContentPanel.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = 16,
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
            Margin = new Avalonia.Thickness(0, 0, 0, 8),
        });
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

    private sealed record ComboValue(string Key, string Display);

    private sealed class SettingsSection
    {
        public string Title { get; }
        private readonly Action build;
        private readonly string[] keywords;

        public SettingsSection(string title, Action build, string[] keywords)
        {
            Title = title;
            this.build = build;
            this.keywords = keywords;
        }

        public void Build() => build();

        public bool Matches(string query) =>
            Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            keywords.Any(keyword => keyword.Contains(query, StringComparison.OrdinalIgnoreCase));
    }
}
