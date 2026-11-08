using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI.Core;

namespace Nomi;

public sealed partial class MainWindow
{
    private string LevelName(string level) => T(level switch { Levels.Fast => "nomiLevelFast", Levels.Applied => "nomiLevelApplied", Levels.Deep => "nomiLevelDeep", _ => "nomiLevelAuto" });

    private UIElement NomiComposer()
    {
        var stack = new StackPanel { Spacing = 6 };
        if (nomiDocuments.Count > 0)
        {
            var files = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            foreach (var document in nomiDocuments.ToArray())
            {
                var chip = Chip(document.Name, "NomiChip", "\uE8A5", "NomiDocument");
                AutomationProperties.SetName(chip, string.Format(CultureInfo.CurrentCulture, T("nomiRemoveDocument"), document.Name));
                chip.Click += (_, _) => { nomiDocuments.Remove(document); BuildNomi(); };
                files.Children.Add(chip);
            }
            stack.Children.Add(files);
        }
        var box = new TextBox
        {
            AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MaxLength = 6000, MinHeight = 44, MaxHeight = 200, FontSize = 15,
            BorderThickness = new Thickness(0), Background = null, Padding = new Thickness(2, 6, 2, 4), PlaceholderText = T("nomiPlaceholder"),
            IsEnabled = nomiRun is null, Text = nomiBox?.Text ?? ""
        };
        foreach (var key in new[] { "TextControlBackgroundFocused", "TextControlBackgroundPointerOver" })
            box.Resources[key] = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
        box.Resources["TextControlBorderThemeThicknessFocused"] = new Thickness(0);
        AutomationProperties.SetName(box, T("nomiPlaceholder"));
        AutomationProperties.SetAutomationId(box, "NomiBox");
        box.PreviewKeyDown += async (_, args) =>
        {
            if (args.Key != VirtualKey.Enter) return;
            if (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down)) return;
            args.Handled = true;
            await SendNomi(box.Text);
        };
        var deep = new Microsoft.UI.Xaml.Input.KeyboardAccelerator { Key = VirtualKey.R, Modifiers = VirtualKeyModifiers.Control };
        deep.Invoked += (_, args) => { args.Handled = true; ToggleDeepLevel(); };
        box.KeyboardAccelerators.Add(deep);
        var escape = new Microsoft.UI.Xaml.Input.KeyboardAccelerator { Key = VirtualKey.Escape };
        escape.Invoked += (_, args) => { if (nomiRun is not null) { args.Handled = true; nomiRun.Cancel(); } };
        box.KeyboardAccelerators.Add(escape);
        nomiBox = box;
        stack.Children.Add(box);
        var row = new Grid { ColumnSpacing = 6 };
        foreach (var width in new[] { GridLength.Auto, GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto, GridLength.Auto })
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = width });
        var attach = new Button { Content = new FontIcon { Glyph = "\uE710", FontSize = 13 }, Style = (Style)Application.Current.Resources["NomiGhost"], Width = 34, Height = 34, Padding = new Thickness(0), IsEnabled = nomiRun is null };
        AutomationProperties.SetName(attach, T("nomiAttach"));
        AutomationProperties.SetAutomationId(attach, "NomiAttach");
        ToolTipService.SetToolTip(attach, T("nomiAttach"));
        attach.Click += async (_, _) => await AttachNomiDocuments();
        row.Children.Add(attach);
        var count = ChatContextNow().Count;
        var sees = Chip(chatHidden ? T("chatHidden") : string.Format(CultureInfo.CurrentCulture, T(count == 1 ? "nomiTasksVisibleOne" : "nomiTasksVisible"), count), "NomiGhost", chatHidden ? "\uE72E" : "\uE8FD", "NomiContext");
        sees.FontSize = 12.5;
        ToolTipService.SetToolTip(sees, chatHidden ? T("chatShow") : T("chatHide"));
        sees.Click += (_, _) => { chatHidden = !chatHidden; BuildNomi(); };
        Grid.SetColumn(sees, 1);
        row.Children.Add(sees);
        var level = new DropDownButton { Content = LevelName(nomiLevel), Style = (Style)Application.Current.Resources["NomiChip"], FontSize = 12.5, CornerRadius = new CornerRadius(12) };
        AutomationProperties.SetName(level, $"{T("nomiLevel")} : {LevelName(nomiLevel)}");
        AutomationProperties.SetAutomationId(level, "NomiLevel");
        level.Flyout = LevelMenu();
        Grid.SetColumn(level, 3);
        row.Children.Add(level);
        var running = nomiRun is not null;
        var send = new Button
        {
            Content = new FontIcon { Glyph = running ? "\uE71A" : "\uE74A", FontSize = 13 },
            Style = (Style)Application.Current.Resources[running ? "NomiChip" : "NomiPrimary"], Width = 34, Height = 34, Padding = new Thickness(0), CornerRadius = new CornerRadius(17)
        };
        AutomationProperties.SetName(send, running ? T("stop") : T("chatSend"));
        AutomationProperties.SetAutomationId(send, running ? "NomiStop" : "NomiSend");
        send.Click += async (_, _) => { if (nomiRun is not null) nomiRun.Cancel(); else await SendNomi(box.Text); };
        Grid.SetColumn(send, 4);
        row.Children.Add(send);
        stack.Children.Add(row);
        return new Border
        {
            Child = stack, MaxWidth = 680, HorizontalAlignment = HorizontalAlignment.Stretch, Background = Palette("NomiSurface"),
            BorderBrush = Palette("NomiLine"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(24),
            Padding = new Thickness(16, 12, 12, 10), Shadow = (Microsoft.UI.Xaml.Media.Shadow)Application.Current.Resources["NomiShadow"], Translation = new System.Numerics.Vector3(0, 0, 12)
        };
    }

    private MenuFlyout LevelMenu()
    {
        var menu = new MenuFlyout();
        foreach (var level in Levels.All)
        {
            var item = new RadioMenuFlyoutItem { Text = LevelName(level), GroupName = "NomiLevel", IsChecked = nomiLevel == level };
            ToolTipService.SetToolTip(item, T($"{LevelKey(level)}Help"));
            AutomationProperties.SetHelpText(item, T($"{LevelKey(level)}Help"));
            AutomationProperties.SetAutomationId(item, $"NomiLevel_{level}");
            item.Click += (_, _) => { nomiLevel = level; BuildNomi(); };
            menu.Items.Add(item);
        }
        return menu;
    }

    private static string LevelKey(string level) => level switch { Levels.Fast => "nomiLevelFast", Levels.Applied => "nomiLevelApplied", Levels.Deep => "nomiLevelDeep", _ => "nomiLevelAuto" };
}
