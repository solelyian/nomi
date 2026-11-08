using System.Globalization;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Nomi;

public sealed partial class MainWindow
{
    private readonly ThreadStore threads = LoadThreads();
    private ChatThread? openThread;
    private string nomiLevel = Levels.Auto;
    private bool historyShown = true;
    private string historyQuery = "";
    private ChatEntry? detailsEntry;
    private readonly List<AttachedDocument> nomiDocuments = [];
    private CancellationTokenSource? nomiRun;
    private ChatEntry? liveEntry;
    private int liveStep;
    private TextBox? nomiBox;

    private static ThreadStore LoadThreads()
    {
        var store = new ThreadStore();
        store.Load();
        return store;
    }

    private void BuildNomi()
    {
        NomiPanel.Children.Clear();
        NomiPanel.ColumnDefinitions.Clear();
        NomiPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(historyShown ? 236 : 0) });
        NomiPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        NomiPanel.ColumnSpacing = historyShown ? 12 : 0;
        if (historyShown) NomiPanel.Children.Add(HistoryColumn());
        var main = new Grid { RowSpacing = 0 };
        main.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        main.Children.Add(NomiHeader());
        var body = (FrameworkElement)(!inference.HasModel && !inference.Ready ? NoModelState() : openThread is null ? NomiWelcome() : NomiThread(openThread));
        Grid.SetRow(body, 1);
        main.Children.Add(body);
        var card = new Border { Child = main, Style = (Style)Application.Current.Resources["NomiCard"], CornerRadius = new CornerRadius(22), Padding = new Thickness(10, 8, 10, 10) };
        Grid.SetColumn(card, 1);
        NomiPanel.Children.Add(card);
    }

    private UIElement HistoryColumn()
    {
        var grid = new Grid { RowSpacing = 6 };
        foreach (var height in new[] { GridLength.Auto, GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto })
            grid.RowDefinitions.Add(new RowDefinition { Height = height });
        var fresh = Chip(T("chatNew"), "NomiChip", "\uE710", "NomiNew");
        fresh.HorizontalAlignment = HorizontalAlignment.Stretch;
        fresh.HorizontalContentAlignment = HorizontalAlignment.Left;
        fresh.FontWeight = FontWeights.SemiBold;
        fresh.Padding = new Thickness(12, 10, 12, 10);
        fresh.CornerRadius = new CornerRadius(13);
        fresh.Click += (_, _) => NewThread();
        grid.Children.Add(fresh);
        var list = new StackPanel { Spacing = 1 };
        var search = new TextBox { PlaceholderText = T("nomiSearchPlaceholder"), Text = historyQuery, FontSize = 13, CornerRadius = new CornerRadius(11), Margin = new Thickness(2, 4, 2, 2) };
        AutomationProperties.SetName(search, T("nomiSearch"));
        AutomationProperties.SetAutomationId(search, "NomiSearch");
        search.TextChanged += (_, _) => { historyQuery = search.Text; FillHistory(list); };
        Grid.SetRow(search, 1);
        grid.Children.Add(search);
        var scroll = new ScrollViewer { Content = list, HorizontalScrollMode = ScrollMode.Disabled, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        AutomationProperties.SetName(list, T("nomiConversations"));
        Grid.SetRow(scroll, 2);
        grid.Children.Add(scroll);
        FillHistory(list);
        var foot = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7, Padding = new Thickness(10, 8, 10, 4) };
        foot.Children.Add(new FontIcon { Glyph = "\uE72E", FontSize = 11, Foreground = Palette("NomiInk3") });
        foot.Children.Add(Text(T("nomiEncrypted"), 11.5, "NomiInk3"));
        Grid.SetRow(foot, 3);
        grid.Children.Add(foot);
        return new Border { Child = grid, Style = (Style)Application.Current.Resources["NomiCard"], CornerRadius = new CornerRadius(22), Padding = new Thickness(10, 12, 10, 8) };
    }
}
