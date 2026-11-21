using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Nomi.Prototype.WinUI.Services;

namespace Nomi.Prototype.WinUI.Views;

public sealed partial class CalculsView : UserControl
{
    public CalculsView()
    {
        this.InitializeComponent();
        RenderRows(verified: false);
    }

    private void OnVerify(object sender, RoutedEventArgs e) => RenderRows(verified: true);

    private void RenderRows(bool verified)
    {
        Rows.Children.Clear();
        var results = verified ? CalcService.Verify(DemoData.CalcRows) : null;
        int i = 0;
        foreach (var (label, expr, expected) in DemoData.CalcRows)
        {
            var computed = results?[i].Computed;
            var match = results?[i].Match ?? true;
            i++;

            var grid = new Grid { Padding = new Thickness(14, 9, 14, 9), ColumnSpacing = 12 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var res = (Style)Application.Current.Resources["CodeText"];
            var labelTb = new TextBlock { Text = label, Style = (Style)Application.Current.Resources["BodyText"], VerticalAlignment = VerticalAlignment.Center };
            grid.Children.Add(labelTb);
            var exprTb = new TextBlock { Text = expr, Style = res, Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"], VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(exprTb, 1);
            grid.Children.Add(exprTb);
            var expectedTb = new TextBlock { Text = CalcService.Format(expected) + " €", Style = res, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(expectedTb, 2);
            grid.Children.Add(expectedTb);

            var computedTb = new TextBlock { Style = res, VerticalAlignment = VerticalAlignment.Center, Text = verified ? CalcService.Format(computed!.Value) + " €" : "—" };
            if (verified && !match) computedTb.Foreground = (Brush)Application.Current.Resources["ErrBrush"];
            Grid.SetColumn(computedTb, 3);
            grid.Children.Add(computedTb);

            var pill = new Border
            {
                Style = (Style)Application.Current.Resources["PillBorder"],
                VerticalAlignment = VerticalAlignment.Center,
                Background = (Brush)Application.Current.Resources[verified ? (match ? "OkSoftBrush" : "ErrSoftBrush") : "NavSelBrush"],
                Child = new TextBlock
                {
                    Style = (Style)Application.Current.Resources["PillText"],
                    Foreground = (Brush)Application.Current.Resources[verified ? (match ? "OkBrush" : "ErrBrush") : "InkSoftBrush"],
                    Text = verified ? (match ? "conforme" : "écart") : "à vérifier",
                },
            };
            Grid.SetColumn(pill, 4);
            grid.Children.Add(pill);
            Rows.Children.Add(grid);
        }
    }
}
