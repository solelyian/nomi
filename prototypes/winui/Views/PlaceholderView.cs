using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Nomi.Prototype.WinUI.Services;

namespace Nomi.Prototype.WinUI.Views;

/// <summary>Vue générique pour les écrans non implémentés du prototype.</summary>
public sealed class PlaceholderView : UserControl
{
    public PlaceholderView(string name)
    {
        var title = new TextBlock
        {
            Text = name,
            Style = (Style)Application.Current.Resources["CardTitle"],
            Margin = new Thickness(0, 0, 0, 8),
        };
        var sub = new TextBlock
        {
            Text = Loc.English
                ? "Screen designed in the v4 mockup — not implemented in this prototype."
                : "Écran dessiné dans la maquette v4 — non implémenté dans ce prototype.",
            Style = (Style)Application.Current.Resources["CaptionText"],
        };
        var card = new Border
        {
            Style = (Style)Application.Current.Resources["GlassCard"],
            Margin = new Thickness(24, 24, 24, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Child = new StackPanel { Children = { title, sub } },
        };
        Content = card;
    }
}
