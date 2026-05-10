using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using WsFiler.Core.KeyMap;
using WsFiler.Presentation.Resources;

namespace WsFiler.App.Views;

public partial class KeyMapDialog : Window
{
    private readonly Dictionary<string, string> defaults;
    private readonly Dictionary<string, string> overrides;
    private readonly ObservableCollection<KeyBindingRow> rows = [];

    public KeyMapDialog()
        : this(new Dictionary<string, string>())
    {
    }

    public KeyMapDialog(IReadOnlyDictionary<string, string> initialOverrides)
    {
        InitializeComponent();

        Title = Strings.Dialog_Keymap_Title;
#pragma warning disable CS0618
        FilterBox.Watermark = Strings.Dialog_Settings_SearchPlaceholder;
#pragma warning restore CS0618
        EditButton.Content = Strings.Dialog_Keymap_Edit;
        ResetButton.Content = Strings.Dialog_Keymap_Reset;
        OkButton.Content = Strings.Dialog_Common_Ok;
        CancelButton.Content = Strings.Dialog_Common_Cancel;
        BindingsGrid.Columns[0].Header = Strings.Dialog_Keymap_Column_Command;
        BindingsGrid.Columns[1].Header = Strings.Dialog_Keymap_Column_Key;

        defaults = DefaultKeyMap.Bindings.ToDictionary(
            binding => binding.CommandId,
            binding => binding.Gesture.ToString(),
            StringComparer.OrdinalIgnoreCase);

        overrides = new Dictionary<string, string>(initialOverrides, StringComparer.OrdinalIgnoreCase);

        BuildRows("");
        BindingsGrid.ItemsSource = rows;

        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        Opened += (_, _) => Dispatcher.UIThread.Post(() => BindingsGrid.Focus());
    }

    private void BuildRows(string filter)
    {
        rows.Clear();
        var allCommandIds = defaults.Keys
            .Union(overrides.Keys, StringComparer.OrdinalIgnoreCase)
            .OrderBy(commandId => commandId, StringComparer.OrdinalIgnoreCase);

        foreach (var commandId in allCommandIds)
        {
            var gesture = ResolveGesture(commandId);
            if (!string.IsNullOrEmpty(filter) &&
                !commandId.Contains(filter, StringComparison.OrdinalIgnoreCase) &&
                !gesture.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            rows.Add(new KeyBindingRow(commandId, gesture));
        }
    }

    private string ResolveGesture(string commandId)
    {
        if (overrides.TryGetValue(commandId, out var custom))
        {
            return custom;
        }

        return defaults.TryGetValue(commandId, out var defaultGesture) ? defaultGesture : "";
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close(null);
        }
    }

    private void OnFilterChanged(object? sender, TextChangedEventArgs e)
    {
        BuildRows(FilterBox.Text?.Trim() ?? "");
    }

    private async void OnEditClick(object? sender, RoutedEventArgs e)
    {
        await EditSelectedAsync();
    }

    private async void OnGridDoubleTapped(object? sender, TappedEventArgs e)
    {
        await EditSelectedAsync();
    }

    private async Task EditSelectedAsync()
    {
        if (BindingsGrid.SelectedItem is not KeyBindingRow row)
        {
            return;
        }

        var capture = new KeyCaptureDialog();
        var captured = await capture.ShowDialog<string?>(this);
        if (string.IsNullOrEmpty(captured))
        {
            return;
        }

        overrides[row.CommandId] = captured;
        BuildRows(FilterBox.Text?.Trim() ?? "");
        var refreshed = rows.FirstOrDefault(item => item.CommandId == row.CommandId);
        if (refreshed is not null)
        {
            BindingsGrid.SelectedItem = refreshed;
        }
    }

    private void OnResetClick(object? sender, RoutedEventArgs e)
    {
        if (BindingsGrid.SelectedItem is not KeyBindingRow row)
        {
            return;
        }

        overrides.Remove(row.CommandId);
        BuildRows(FilterBox.Text?.Trim() ?? "");
        var refreshed = rows.FirstOrDefault(item => item.CommandId == row.CommandId);
        if (refreshed is not null)
        {
            BindingsGrid.SelectedItem = refreshed;
        }
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        Close(new Dictionary<string, string>(overrides, StringComparer.OrdinalIgnoreCase));
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

}

public sealed record KeyBindingRow(string CommandId, string Gesture);
