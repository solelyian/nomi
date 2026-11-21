using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Nomi.Prototype.WinUI.Views;

public sealed partial class AccueilView : UserControl
{
    private static readonly (string Time, string What, string Tag)[] Today =
    {
        ("09:30", "TD Analyse — salle B204 (feuille 7 à rendre)", "Cours"),
        ("14:00", "Relecture facture.ts avec Nomi — 3 points à trancher", "Studio"),
        ("17:30", "Fiche IPP : relire la section « Conduite »", "Études"),
    };

    public AccueilView()
    {
        this.InitializeComponent();
        foreach (var (time, what, tag) in Today)
        {
            var row = new Grid { ColumnSpacing = 12 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var t = new TextBlock { Text = time, Style = (Style)Application.Current.Resources["CodeText"], Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] };
            var w = new TextBlock { Text = what, Style = (Style)Application.Current.Resources["BodyText"], VerticalAlignment = VerticalAlignment.Center };
            var pill = new Border { Style = (Style)Application.Current.Resources["PillBorder"], Child = new TextBlock { Text = tag, Style = (Style)Application.Current.Resources["PillText"] }, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(w, 1); Grid.SetColumn(pill, 2);
            row.Children.Add(t); row.Children.Add(w); row.Children.Add(pill);
            TodayList.Children.Add(row);
        }
    }

    private void OnGo(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string key })
            Nav.Go?.Invoke(key);
    }
}
