using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Nomi.Prototype.WinUI.Services;
using Nomi.Prototype.WinUI.Views;

namespace Nomi.Prototype.WinUI;

/// <summary>Pont de navigation interne — les vues déclenchent Nav.Go("code") etc.</summary>
public static class Nav
{
    public static System.Action<string>? Go;
    /// <summary>Reconstruit le chrome localisé (nav, badge, bouton langue) après un changement de Loc.English.</summary>
    public static System.Action? Relocalize;
}

public sealed partial class MainWindow : Window
{
    // clé nav → vue (null = espace réservé « bientôt »)
    private static readonly (string Group, (string Key, string LocKey, System.Type? View)[] Items)[] NavGroups =
    {
        ("nomi", new[] { ("accueil", "accueil", typeof(AccueilView)) }),
        ("journee", new (string, string, System.Type?)[] {
            ("aujourdhui", "aujourdhui", null), ("tableau", "tableau", null), ("routines", "routines", null) }),
        ("studio", new (string, string, System.Type?)[] {
            ("code", "code", typeof(CodeView)),
            ("calculs", "verifier", typeof(CalculsView)),
            ("focus", "focus", null) }),
        ("etudes", new (string, string, System.Type?)[] {
            ("cours", "cours", null),
            ("transcriptions", "transcriptions", typeof(TranscriptionView)),
            ("fiches", "fiches", typeof(FicheView)) }),
        ("autres", new (string, string, System.Type?)[] {
            ("bibliotheque", "bibliotheque", typeof(BibliothequeView)),
            ("assistant", "assistant", null),
            ("parametres", "parametres", typeof(ParametresView)) }),
    };

    private readonly Dictionary<Button, (string Key, System.Type? View)> _navButtons = new();
    private Button? _selected;

    public MainWindow()
    {
        this.InitializeComponent();
        Title = "Nomi — prototype";
        ExtendsContentIntoTitleBar = false;
        BuildNav();
        Nav.Go = Navigate;
        Nav.Relocalize = Relocalize;
        Navigate("accueil");
    }

    private void BuildNav()
    {
        NavPanel.Children.Clear();
        _navButtons.Clear();
        foreach (var (group, items) in NavGroups)
        {
            if (group is not ("nomi" or "autres"))
                NavPanel.Children.Add(new TextBlock { Style = Res("OverlineText"), Text = Loc.T(group).ToUpperInvariant(), Margin = new Thickness(14, 14, 0, 4) });
            foreach (var (key, locKey, view) in items)
            {
                        var btn = new Button { Style = Res("NavItem"), Content = Loc.T(locKey) };
                AutomationProperties.SetName(btn, Loc.T(locKey));
                _navButtons[btn] = (key, view);
                btn.Click += (s, _) => Navigate(_navButtons[(Button)s].Key);
                NavPanel.Children.Add(btn);
            }
        }
    }

    private void Navigate(string key)
    {
        foreach (var (btn, (k, view)) in _navButtons)
        {
            bool sel = k == key;
            btn.Style = Res(sel ? "NavItemSelected" : "NavItem");
            if (sel) _selected = btn;
        }
        var entry = _navButtons[_selected!];
        ContentFrame.Content = entry.View is { } t
            ? System.Activator.CreateInstance(t)
            : new PlaceholderView(Loc.T(key));
    }

    private static Style Res(string key) => (Style)Application.Current.Resources[key];

    private void OnThemeToggle(object sender, RoutedEventArgs e)
    {
        var root = (FrameworkElement)Content;
        root.RequestedTheme = root.ActualTheme == ElementTheme.Dark ? ElementTheme.Light : ElementTheme.Dark;
        ThemeToggle.Content = root.ActualTheme == ElementTheme.Dark ? "☀" : "☾";
    }

    private void Relocalize()
    {
        LangToggle.Content = Loc.English ? "EN" : "FR";
        DemoBadgeText.Text = Loc.T("demo");
        BuildNav();
    }

    private void OnLangToggle(object sender, RoutedEventArgs e)
    {
        Loc.English = !Loc.English;
        Relocalize();
        Navigate("accueil");
    }
}
