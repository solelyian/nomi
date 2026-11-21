using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Nomi.Prototype.WinUI.Views;

public sealed partial class BibliothequeView : UserControl
{
    private static readonly (string Kind, string Title, string Meta, string Nav)[] Items =
    {
        ("Transcription", "Analyse — séance 8", "42:17 · aujourd'hui", "transcriptions"),
        ("Fiche", "IPP — insuffisance pondérale", "6 sections · hier", "fiches"),
        ("Cours", "Anatomie — IPP", "transcription 38 min", "cours"),
        ("Transcription", "TD Statistiques — tests", "31:40 · mardi", "transcriptions"),
        ("Fiche", "Théorèmes d'analyse", "8 sections · semaine dernière", "fiches"),
        ("Cours", "Physiologie — néphron", "notes + audio", "cours"),
    };

    public BibliothequeView()
    {
        this.InitializeComponent();
        var rows = (Items.Length + 2) / 3;
        for (int c = 0; c < 3; c++)
            Cards.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (int r = 0; r < rows; r++)
            Cards.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        for (int i = 0; i < Items.Length; i++)
        {
            var (kind, title, meta, nav) = Items[i];
            var stack = new StackPanel { Spacing = 6 };
            stack.Children.Add(new Border
            {
                Style = (Style)Application.Current.Resources["PillBorder"],
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = new TextBlock { Text = kind.ToUpperInvariant(), Style = (Style)Application.Current.Resources["PillText"], FontSize = 10 },
            });
            stack.Children.Add(new TextBlock { Text = title, Style = (Style)Application.Current.Resources["CardTitle"], FontSize = 14 });
            stack.Children.Add(new TextBlock { Text = meta, Style = (Style)Application.Current.Resources["CaptionText"] });
            var btn = new Button { Style = (Style)Application.Current.Resources["GhostButton"], Content = "Ouvrir →", Tag = nav, HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(0, 4, 8, 4) };
            btn.Click += (s, _) => Nav.Go?.Invoke((string)((Button)s!).Tag);
            stack.Children.Add(btn);

            var card = new Border
            {
                Style = (Style)Application.Current.Resources["GlassCard"],
                Margin = new Thickness(0, 0, 12, 12),
                Child = stack,
            };
            Grid.SetRow(card, i / 3);
            Grid.SetColumn(card, i % 3);
            Cards.Children.Add(card);
        }
    }
}
