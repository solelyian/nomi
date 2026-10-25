using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Nomi;

public sealed partial class MainWindow
{
    private Grid? welcome;

    private string WelcomePreview(string mode)
    {
        var day = profile.Day;
        var start = day.At(Now, day.LunchEnd).AddMinutes(15);
        var now = day.At(Now, day.LunchStart).AddMinutes(-30);
        return mode == NumberModes.Figures
            ? string.Format(System.Globalization.CultureInfo.CurrentCulture, T("welcomePreviewFigures"),
                Readable.Span(start, start.AddMinutes(40), now, mode, language, day), Readable.Duration(40, mode, language))
            : string.Format(System.Globalization.CultureInfo.CurrentCulture, T("welcomePreview"),
                Readable.Clock(start, now, mode, language, day), Readable.Duration(40, mode, language));
    }

    private void ShowWelcome()
    {
        if (welcome is not null) Root.Children.Remove(welcome);
        var choice = profile.Numbers;
        var stack = new StackPanel { Spacing = 14, MaxWidth = 520 };
        var title = Text(T("welcomeTitle"), 24);
        title.FontWeight = FontWeights.SemiBold;
        AutomationProperties.SetHeadingLevel(title, AutomationHeadingLevel.Level1);
        stack.Children.Add(title);
        stack.Children.Add(Text(T("welcomeIntro"), 13.5, "NomiInk2"));
        var task = Text(T("welcomeTask"), 15);
        task.FontWeight = FontWeights.SemiBold;
        var preview = Text(WelcomePreview(choice), 14, "NomiInk");
        var sample = new StackPanel { Spacing = 6, Padding = new Thickness(14, 12, 14, 12) };
        sample.Children.Add(task);
        sample.Children.Add(preview);
        AutomationProperties.SetLiveSetting(preview, AutomationLiveSetting.Polite);
        var options = new RadioButtons { MaxColumns = 3 };
        AutomationProperties.SetName(options, T("welcomeTitle"));
        foreach (var mode in NumberModes.All) options.Items.Add(NumberModeName(mode));
        options.SelectedIndex = Array.IndexOf(NumberModes.All, choice);
        options.SelectionChanged += (_, _) =>
        {
            if (options.SelectedIndex < 0) return;
            choice = NumberModes.All[options.SelectedIndex];
            preview.Text = WelcomePreview(choice);
        };
        stack.Children.Add(options);
        stack.Children.Add(new Border { Background = Palette("NomiGlassSoft"), CornerRadius = new CornerRadius(12), Child = sample });
        stack.Children.Add(Text(T("welcomeNote"), 12, "NomiInk3"));
        var start = Chip(T("welcomeStart"), "NomiChip", null, "WelcomeStart");
        start.HorizontalAlignment = HorizontalAlignment.Left;
        start.Click += (_, _) =>
        {
            profile.Numbers = choice;
            profile.Onboarded = true;
            SaveProfile();
            Root.Children.Remove(welcome);
            welcome = null;
            Refresh();
            ShowToast(T("welcomeSaved"), null, null);
        };
        stack.Children.Add(start);
        var card = Glass(stack, 28, true);
        card.HorizontalAlignment = HorizontalAlignment.Center;
        card.VerticalAlignment = VerticalAlignment.Center;
        card.Margin = new Thickness(24);
        welcome = new Grid { Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(110, 0, 0, 0)) };
        AutomationProperties.SetAutomationId(welcome, "Welcome");
        Grid.SetRowSpan(welcome, 16);
        Grid.SetColumnSpan(welcome, 16);
        welcome.Children.Add(card);
        Root.Children.Add(welcome);
        options.Focus(FocusState.Programmatic);
    }
}
