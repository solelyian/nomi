using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Nomi;

public sealed partial class MainWindow
{
    private static readonly ConditionalWeakTable<DependencyObject, object> baseSizes = new();
    private Tier tier = Tokens.Wide;
    private bool asideOpen;

    private bool AsideWanted => view is not ("nomi" or "today");

    private void WireLayout()
    {
        Root.SizeChanged += (_, args) =>
        {
            var next = Tokens.For(args.NewSize.Width);
            if (next == tier) return;
            tier = next;
            asideOpen = false;
            historyShown = next.Name != Tokens.Compact.Name;
            Refresh();
        };
    }

    private void LayOut()
    {
        SidebarColumn.Width = new GridLength(tier.Rail);
        Sidebar.Padding = tier.RailLabels ? new Thickness(10, 12, 10, 12) : new Thickness(6, 10, 6, 10);
        WorkTitle.Visibility = ActionsTitle.Visibility = RailNote.Visibility = tier.RailLabels ? Visibility.Visible : Visibility.Collapsed;
        Body.Padding = new Thickness(tier.Gap, 4, tier.Gap, tier.Gap);
        var docked = tier.AsideDocked && AsideWanted;
        AsideColumn.Width = new GridLength(docked ? tier.Aside : 0);
        MainArea.Margin = new Thickness(tier.Gap, 0, docked ? tier.Gap : 0, 0);
        Grid.SetColumn(Aside, docked ? 2 : 1);
        Aside.Width = docked ? double.NaN : tier.Aside;
        Aside.HorizontalAlignment = docked ? HorizontalAlignment.Stretch : HorizontalAlignment.Right;
        Canvas.SetZIndex(Aside, docked ? 0 : 20);
        Aside.Visibility = AsideWanted && (docked || asideOpen) ? Visibility.Visible : Visibility.Collapsed;
        AsideScrim.Visibility = !docked && asideOpen && AsideWanted ? Visibility.Visible : Visibility.Collapsed;
        ScaleText(Body);
    }

    private Button? AsideButton()
    {
        if (tier.AsideDocked || !AsideWanted) return null;
        var button = Chip(T("panel"), "NomiChip", "\uE8A0", "AsideToggle");
        AutomationProperties.SetItemStatus(button, asideOpen ? T("current") : "");
        button.Click += (_, _) => ToggleAside(button);
        return button;
    }

    private void ToggleAside(FrameworkElement? origin)
    {
        asideOpen = !asideOpen;
        LayOut();
        if (asideOpen) Morph(Aside, origin);
    }

    private void ScaleText(DependencyObject root)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            switch (child)
            {
                case TextBlock text: text.FontSize = Scaled(text, text.FontSize); break;
                case FontIcon icon: icon.FontSize = Scaled(icon, icon.FontSize); break;
            }
            ScaleText(child);
        }
    }

    private double Scaled(DependencyObject element, double current)
    {
        var size = (double)baseSizes.GetValue(element, _ => current);
        var ratio = size >= 24 ? tier.Title / Tokens.Wide.Title : size >= 16 ? tier.Heading / Tokens.Wide.Heading : tier.Body / Tokens.Wide.Body;
        return Math.Max(10, Math.Round(size * ratio));
    }
}
