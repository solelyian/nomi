using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Nomi.Prototype.WinUI.Services;

namespace Nomi.Prototype.WinUI.Views;

public sealed partial class ParametresView : UserControl
{
    private static readonly (string Name, string Detail, string State, string StateBrush)[] ModelRows =
    {
        ("Qwen3 8B", "Raisonnement, réponses — ~5 Go", "Chargé", "OkSoftBrush"),
        ("Gemma 4 E4B", "Vision — ~6 Go", "Présent", "InfoSoftBrush"),
        ("Whisper small", "Voix → texte — ~488 Mo", "À télécharger", "WarnSoftBrush"),
    };

    private static readonly (string Label, string Detail)[] ReadRows =
    {
        ("Vitesse de lecture", "Synthèse vocale — 1,0× (réel quand Whisper/TTS seront branchés)"),
        ("Nombres", "Arrondi bancaire à 2 décimales — réel (moteur CalcService)"),
        ("Données", "Chiffrées DPAPI sur cette machine — rien ne part en ligne"),
    };

    private bool _ready;

    public ParametresView()
    {
        this.InitializeComponent();
        ThemeSwitch.IsOn = App.Current.RequestedTheme == ApplicationTheme.Dark;
        LangSwitch.IsOn = Loc.English;
        _ready = true;
        foreach (var (name, detail, state, brush) in ModelRows)
        {
            var grid = new Grid { Padding = new Thickness(0, 6, 0, 6), ColumnSpacing = 12 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var left = new StackPanel();
            left.Children.Add(new TextBlock { Text = name, Style = (Style)Application.Current.Resources["BodyText"], FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            left.Children.Add(new TextBlock { Text = detail, Style = (Style)Application.Current.Resources["CaptionText"] });
            grid.Children.Add(left);
            var pill = new Border
            {
                Style = (Style)Application.Current.Resources["PillBorder"], VerticalAlignment = VerticalAlignment.Center,
                Background = (Brush)Application.Current.Resources[brush],
                Child = new TextBlock { Text = state, Style = (Style)Application.Current.Resources["PillText"] },
            };
            Grid.SetColumn(pill, 1);
            grid.Children.Add(pill);
            Models.Children.Add(grid);
        }
        foreach (var (label, detail) in ReadRows)
        {
            var s = new StackPanel { Margin = new Thickness(0, 6, 0, 6) };
            s.Children.Add(new TextBlock { Text = label, Style = (Style)Application.Current.Resources["BodyText"], FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            s.Children.Add(new TextBlock { Text = detail, Style = (Style)Application.Current.Resources["CaptionText"] });
            Reading.Children.Add(s);
        }
    }

    private void OnThemeSwitched(object sender, RoutedEventArgs e)
    {
        // IsOn assigné dans le ctor déclenche Toggled avant l'attachement au XamlRoot.
        if (!_ready || XamlRoot is null) return;
        var root = (FrameworkElement)XamlRoot.Content;
        root.RequestedTheme = ThemeSwitch.IsOn ? ElementTheme.Dark : ElementTheme.Light;
    }

    private void OnLangSwitched(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        Loc.English = LangSwitch.IsOn;
        Nav.Relocalize?.Invoke();
        Nav.Go?.Invoke("accueil");
    }
}
