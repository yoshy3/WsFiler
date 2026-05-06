using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System;
using WsFiler.Presentation.ViewModels;

namespace WsFiler.App.Views;

public partial class SortDialog : Window
{
    public SortDialog(PaneSortField currentField, bool currentAscending)
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);

        switch (currentField)
        {
            case PaneSortField.Extension:  SortExtension.IsChecked  = true; break;
            case PaneSortField.Date:       SortDate.IsChecked       = true; break;
            case PaneSortField.Size:       SortSize.IsChecked       = true; break;
            case PaneSortField.Attributes: SortAttributes.IsChecked = true; break;
            case PaneSortField.None:       SortNone.IsChecked       = true; break;
            default:                       SortName.IsChecked       = true; break;
        }

        OrderAscending.IsChecked  = currentAscending;
        OrderDescending.IsChecked = !currentAscending;

        Opened += (_, _) => GetActiveFieldRadioButton().Focus();
    }

    private RadioButton GetActiveFieldRadioButton() => SortExtension.IsChecked  == true ? SortExtension
                                                      : SortDate.IsChecked       == true ? SortDate
                                                      : SortSize.IsChecked       == true ? SortSize
                                                      : SortAttributes.IsChecked == true ? SortAttributes
                                                      : SortNone.IsChecked       == true ? SortNone
                                                      : SortName;

    private RadioButton[] FieldRadioButtons => [SortName, SortExtension, SortDate, SortSize, SortAttributes, SortNone];

    private void OnOkClick(object? sender, RoutedEventArgs e) => CloseWithResult();

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close(null);
        }
        else if (e.Key == Key.Enter)
        {
            e.Handled = true;
            CloseWithResult();
        }
        else if (e.Key is Key.Up or Key.Down && IsFieldRadioButtonFocused())
        {
            e.Handled = true;
            MoveFieldSelection(e.Key == Key.Down ? 1 : -1);
        }
    }

    private bool IsFieldRadioButtonFocused() => Array.Exists(FieldRadioButtons, radioButton => radioButton.IsFocused);

    private void MoveFieldSelection(int offset)
    {
        var radioButtons = FieldRadioButtons;
        var currentIndex = Array.FindIndex(radioButtons, radioButton => radioButton.IsChecked == true);
        if (currentIndex < 0)
        {
            currentIndex = 0;
        }

        var nextIndex = (currentIndex + offset + radioButtons.Length) % radioButtons.Length;
        radioButtons[nextIndex].IsChecked = true;
        radioButtons[nextIndex].Focus();
    }

    private void CloseWithResult()
    {
        var field = SortExtension.IsChecked  == true ? PaneSortField.Extension
                  : SortDate.IsChecked       == true ? PaneSortField.Date
                  : SortSize.IsChecked       == true ? PaneSortField.Size
                  : SortAttributes.IsChecked == true ? PaneSortField.Attributes
                  : SortNone.IsChecked       == true ? PaneSortField.None
                  : PaneSortField.Name;

        var ascending = OrderAscending.IsChecked == true;
        Close((field, ascending));
    }
}
