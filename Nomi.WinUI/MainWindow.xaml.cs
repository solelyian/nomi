using System.Globalization;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.Storage.Pickers;

namespace Nomi;

public sealed partial class MainWindow : Window
{
    private readonly Dictionary<string, Catalog> catalogs = new()
    {
        ["fr"] = Catalog.Load("fr"),
        ["en"] = Catalog.Load("en")
    };

    private string language = "fr";
    private string page = "home";
    private string? actionId;
    private string? contextId;
    private string homeIntent = "understand";
    private bool compact;
    private bool summary;
    private readonly OllamaClient inference = new();
    private readonly Dictionary<string, Draft> drafts = new();
    private CancellationTokenSource? generation;
    private CancellationTokenSource? documentReading;
    private Draft? activeDraft;
    private bool updating;
    private ContentDialog? palette;
    private Catalog Current => catalogs[language];

    public MainWindow()
    {
        InitializeComponent();
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1280, 940));
        Closed += (_, _) =>
        {
            CancelGeneration();
            inference.Dispose();
        };
        Refresh();
    }

    private string T(string key) => Current.Labels[key];

    private void CancelGeneration()
    {
        if (activeDraft is not null) activeDraft.Status = "stopped";
        generation?.Cancel();
        documentReading?.Cancel();
    }

    private static TextBlock Text(string value, double size = 14)
    {
        return new TextBlock
        {
            Text = value,
            FontSize = size,
            TextWrapping = TextWrapping.Wrap
        };
    }

    private static TextBlock Heading(string value, bool primary = false)
    {
        var text = Text(value, primary ? 36 : 19);
        text.FontWeight = FontWeights.SemiBold;
        AutomationProperties.SetHeadingLevel(
            text, primary ? AutomationHeadingLevel.Level1 : AutomationHeadingLevel.Level2);
        return text;
    }

    private Button Button(string label, RoutedEventHandler handler, bool primary = false)
    {
        var button = new Button
        {
            Content = Text(label),
            MinHeight = 44,
            Padding = new Thickness(16, 10, 16, 10)
        };
        if (primary)
        {
            button.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
        }
        AutomationProperties.SetName(button, label);
        button.Click += handler;
        return button;
    }

    private void Refresh()
    {
        updating = true;
        Root.Language = Current.Locale;
        Title = $"Nomi — {T(page == "action" ? "workspace" : page)}";
        LanguagePicker.Header = T("language");
        AutomationProperties.SetName(LanguagePicker, T("language"));
        LanguagePicker.SelectedIndex = language == "fr" ? 0 : 1;
        AutomationProperties.SetName(CommandButton, T("openCommand"));
        ToolTipService.SetToolTip(CommandButton, T("openCommand"));
        BrandCaption.Text = T("localOnly");
        PrototypeLabel.Text = T("prototype");
        PrototypeNote.Text = T("prototypeNote");
        Navigation.MenuItems.Clear();
        foreach (var (id, glyph) in new[] { ("home", "\uE80F"), ("spaces", "\uE8F1"), ("preferences", "\uE713") })
        {
            var item = new NavigationViewItem
            {
                Content = T(id),
                Tag = id,
                Icon = new FontIcon { Glyph = glyph }
            };
            Navigation.MenuItems.Add(item);
            if (id == page)
            {
                Navigation.SelectedItem = item;
            }
        }
        AutomationProperties.SetName(Navigation, T("navigation"));
        Breadcrumb.Text = $"{T("workspace")}  /  {T(page == "home" ? "overview" : page == "action" ? "action" : page)}";
        PageBody.Children.Clear();
        PageBody.Spacing = compact ? 14 : 24;
        if (page == "preferences") ShowPreferences();
        else if (page == "spaces") ShowSpaces();
        else if (page == "action") ShowAction();
        else ShowHome();
        updating = false;
    }

    private void Navigate(string destination, string? action = null, string? context = null)
    {
        CancelGeneration();
        page = destination;
        actionId = action;
        contextId = context;
        Refresh();
        PageScroll.ChangeView(null, 0, null);
        DispatcherQueue.TryEnqueue(() =>
        {
            var first = PageBody.Children.OfType<Button>().FirstOrDefault();
            first?.Focus(FocusState.Programmatic);
        });
    }

    private void ShowHome()
    {
        ShowAction(true);
        PageBody.Children.Add(Heading(T("starterTitle")));
        PageBody.Children.Add(CardGrid(new[]
        {
            ("verify", "tryVerify"), ("plan", "tryPlan"), ("learn", "tryLearn")
        }.Select(item => Button(T(item.Item2), (_, _) =>
        {
            CancelGeneration();
            homeIntent = item.Item1;
            drafts[$"{language}:home"].Prompt = Current.Actions.Single(action => action.Id == homeIntent).Prompt;
            Refresh();
            PageBody.Children.OfType<TextBox>().FirstOrDefault()?.Focus(FocusState.Programmatic);
        })), 3));
        PageBody.Children.Add(Button(T("allSpaces"), (_, _) => Navigate("spaces")));
        var resumable = drafts.Where(item => item.Key.StartsWith($"{language}:", StringComparison.Ordinal)
            && item.Key != $"{language}:home" && !string.IsNullOrWhiteSpace(item.Value.Prompt)).TakeLast(3).Reverse().ToArray();
        if (resumable.Length > 0)
        {
            PageBody.Children.Add(Heading(T("resumeTitle")));
            PageBody.Children.Add(Text(T("sessionOnly"), 12));
            foreach (var item in resumable)
            {
                var parts = item.Key.Split(':');
                var action = Current.Actions.Single(entry => entry.Id == parts[1]);
                var resume = Button(action.Title, (_, _) => Navigate("action", action.Id, parts[2]));
                var label = Text($"{action.Title} · {item.Value.Prompt}", 13);
                label.MaxLines = 2;
                label.TextTrimming = TextTrimming.CharacterEllipsis;
                resume.Content = label;
                resume.HorizontalAlignment = HorizontalAlignment.Stretch;
                resume.HorizontalContentAlignment = HorizontalAlignment.Left;
                PageBody.Children.Add(resume);
            }
        }
    }

    private Button Card(string title, string description, Action open)
    {
        var content = new StackPanel { Spacing = compact ? 8 : 14 };
        content.Children.Add(Text("↗", 20));
        var name = Text(title, 18);
        name.FontWeight = FontWeights.SemiBold;
        content.Children.Add(name);
        content.Children.Add(Text(description, 13));
        var button = new Button
        {
            Content = content,
            Padding = new Thickness(compact ? 18 : 24),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8)
        };
        AutomationProperties.SetName(button, title);
        AutomationProperties.SetHelpText(button, description);
        button.Click += (_, _) => open();
        return button;
    }

    private static Grid CardGrid(IEnumerable<Button> buttons, int maximumColumns)
    {
        var grid = new Grid { ColumnSpacing = 14, RowSpacing = 14 };
        foreach (var button in buttons) grid.Children.Add(button);
        void Reflow(double width)
        {
            var count = Math.Clamp((int)(width / 260), 1, maximumColumns);
            if (grid.ColumnDefinitions.Count == count) return;
            grid.ColumnDefinitions.Clear();
            grid.RowDefinitions.Clear();
            for (var column = 0; column < count; column++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }
            for (var index = 0; index < grid.Children.Count; index++)
            {
                if (index % count == 0)
                {
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                }
                var child = (FrameworkElement)grid.Children[index];
                Grid.SetRow(child, index / count);
                Grid.SetColumn(child, index % count);
            }
        }
        Reflow(0);
        grid.SizeChanged += (_, args) => Reflow(args.NewSize.Width);
        return grid;
    }

    private Grid ContextGrid()
    {
        return CardGrid(Current.Contexts.Select(context =>
            Card(context.Title, context.Description,
                () => Navigate("action", context.Action, context.Id))), 2);
    }

    private void ShowSpaces()
    {
        PageBody.Children.Add(Heading(T("spaces"), true));
        PageBody.Children.Add(Text(T("spacesIntro")));
        PageBody.Children.Add(ContextGrid());
        PageBody.Children.Add(Text(T("demoNotice"), 12));
    }

    private void ShowAction(bool home = false)
    {
        var action = Current.Actions.Single(item => item.Id == (home ? homeIntent : actionId));
        var context = Current.Contexts.SingleOrDefault(item => item.Id == contextId);
        Breadcrumb.Text = $"{T("workspace")}  /  {(home ? T("overview") : action.Title)}";
        if (!home) PageBody.Children.Add(Button(T("back"), (_, _) => Navigate("home")));
        PageBody.Children.Add(Text(home ? T("greeting") : context?.Title ?? T("spaceTag"), 12));
        PageBody.Children.Add(Heading(home ? T("headline") : action.Title, true));
        PageBody.Children.Add(Text(home ? T("intro") : action.Description));
        RadioButtons? intentPicker = null;
        var intentDescription = Text(action.Description, 12);
        if (home)
        {
            intentPicker = new RadioButtons
            {
                Header = T("intent"),
                ItemsSource = Current.Actions.Select(item => item.Title).ToArray(),
                SelectedIndex = Array.FindIndex(Current.Actions, item => item.Id == homeIntent),
                MaxColumns = 3
            };
            AutomationProperties.SetName(intentPicker, T("intent"));
            PageBody.Children.Add(intentPicker);
            PageBody.Children.Add(intentDescription);
        }
        var key = home ? $"{language}:home" : $"{language}:{actionId}:{contextId}";
        if (!drafts.TryGetValue(key, out var draft))
        {
            draft = new Draft();
            drafts[key] = draft;
        }
        var input = new TextBox
        {
            Header = T("requestLabel"),
            PlaceholderText = action.Prompt,
            Text = draft.Prompt,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MaxLength = 6000,
            MinHeight = 130,
            MaxHeight = 400
        };
        AutomationProperties.SetName(input, T("requestLabel"));
        AutomationProperties.SetHelpText(input, T("requestHelp"));
        input.TextChanged += (_, _) => draft.Prompt = input.Text;
        if (intentPicker is not null)
        {
            intentPicker.SelectionChanged += (_, _) =>
            {
                if (intentPicker.SelectedIndex < 0) return;
                action = Current.Actions[intentPicker.SelectedIndex];
                homeIntent = action.Id;
                intentDescription.Text = action.Description;
                input.PlaceholderText = action.Prompt;
            };
        }
        PageBody.Children.Add(input);
        var documentsPanel = CreateDocumentPanel(draft);
        PageBody.Children.Add(documentsPanel);
        var formatPicker = new ComboBox
        {
            Header = T("nextFormat"),
            ItemsSource = new[] { T("steps"), T("summary") },
            SelectedIndex = summary ? 1 : 0,
            MinHeight = 44
        };
        AutomationProperties.SetName(formatPicker, T("response"));
        formatPicker.SelectionChanged += (_, _) => summary = formatPicker.SelectedIndex == 1;
        PageBody.Children.Add(formatPicker);
        var example = Button(T("loadExample"), (_, _) => { input.Text = action.Prompt; input.Focus(FocusState.Programmatic); });
        var status = Text(draft.Status == "idle" ? "" : T(draft.Status), 13);
        AutomationProperties.SetLiveSetting(status, AutomationLiveSetting.Polite);
        void SetStatus(string value)
        {
            draft.Status = value;
            status.Text = T(value);
            FrameworkElementAutomationPeer.FromElement(status)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        }
        var output = Text(draft.Output, 16);
        var policyRules = Text(string.Join(" · ", draft.Rules.Select(rule => T($"policy-{rule}"))), 12);
        output.IsTextSelectionEnabled = true;
        var result = new StackPanel
        {
            Spacing = compact ? 12 : 20,
            Visibility = draft.Output.Length > 0 ? Visibility.Visible : Visibility.Collapsed
        };
        var copyStatus = Text("", 12);
        AutomationProperties.SetLiveSetting(copyStatus, AutomationLiveSetting.Polite);
        var copy = Button(T("copy"), (_, _) =>
        {
            copyStatus.Text = CopyResponse(output.Text);
            FrameworkElementAutomationPeer.FromElement(copyStatus)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        });
        var send = Button(T("send"), (_, _) => { }, true);
        var stop = Button(T("stop"), (_, _) => CancelGeneration());
        stop.Visibility = Visibility.Collapsed;
        async Task Send()
        {
            if (generation is not null) return;
            if (documentReading is not null) { SetStatus("documentReading"); return; }
            if (string.IsNullOrWhiteSpace(input.Text) && draft.Documents.Count == 0) { SetStatus("invalid-request"); input.Focus(FocusState.Programmatic); return; }
            using var controller = new CancellationTokenSource();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(180));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(controller.Token, timeout.Token);
            generation = controller;
            activeDraft = draft;
            send.IsEnabled = false;
            example.IsEnabled = false;
            formatPicker.IsEnabled = false;
            documentsPanel.IsEnabled = false;
            if (intentPicker is not null) intentPicker.IsEnabled = false;
            input.IsReadOnly = true;
            stop.Visibility = Visibility.Visible;
            copyStatus.Text = "";
            draft.Output = output.Text = "";
            draft.Rules = [];
            policyRules.Text = "";
            result.Visibility = Visibility.Collapsed;
            SetStatus("generating");
            try
            {
                var adapted = await inference.GenerateNomiAsync(input.Text, action.Id, language,
                    summary ? "summary" : "steps", linked.Token, draft.Documents.ToArray());
                linked.Token.ThrowIfCancellationRequested();
                draft.Output = output.Text = adapted.Text;
                draft.Rules = adapted.Changes;
                policyRules.Text = string.Join(" · ", draft.Rules.Select(rule => T($"policy-{rule}")));
                result.Visibility = Visibility.Visible;
                SetStatus(adapted.Changes.Length > 0 ? "policy-adjusted" : "policy-applied");
            }
            catch (OperationCanceledException) { SetStatus(timeout.IsCancellationRequested ? "timeout" : "stopped"); }
            catch (InferenceException exception) { SetStatus(exception.Message); }
            catch (HttpRequestException) { SetStatus("engine-unavailable"); }
            catch (IOException) { SetStatus("incomplete-response"); }
            catch (JsonException) { SetStatus("invalid-response"); }
            catch (ObjectDisposedException) when (controller.IsCancellationRequested) { SetStatus("stopped"); }
            finally
            {
                generation = null;
                activeDraft = null;
                send.IsEnabled = true;
                example.IsEnabled = true;
                formatPicker.IsEnabled = true;
                documentsPanel.IsEnabled = true;
                if (intentPicker is not null) intentPicker.IsEnabled = true;
                input.IsReadOnly = false;
                stop.Visibility = Visibility.Collapsed;
            }
        }
        send.Click += async (_, _) => await Send();
        var submit = new KeyboardAccelerator { Key = VirtualKey.Enter, Modifiers = VirtualKeyModifiers.Control };
        submit.Invoked += async (_, args) => { args.Handled = true; await Send(); };
        input.KeyboardAccelerators.Add(submit);
        var controls = CardGrid(new[] { example, send, stop }, 3);
        PageBody.Children.Add(controls);
        PageBody.Children.Add(Text(T("requestHelp"), 12));
        PageBody.Children.Add(status);
        result.Children.Add(Heading(T("resultTitle")));
        result.Children.Add(copy);
        result.Children.Add(copyStatus);
        result.Children.Add(output);
        result.Children.Add(Text(T("resultReminder"), 12));
        var responseDetails = new StackPanel { Spacing = 12 };
        responseDetails.Children.Add(policyRules);
        responseDetails.Children.Add(Text($"{T("resultTag")} · {inference.Model}", 11));
        result.Children.Add(new Expander
        {
            Header = T("responseDetails"),
            Content = responseDetails,
            HorizontalAlignment = HorizontalAlignment.Stretch
        });
        result.Children.Add(Button(T("editRequest"), (_, _) => input.Focus(FocusState.Programmatic)));
        PageBody.Children.Add(result);
        PageBody.Children.Add(new Expander
        {
            Header = T("localProcessing"),
            Content = Text($"{inference.Model} · Ollama\n\n{T("demoNotice")}", 12),
            HorizontalAlignment = HorizontalAlignment.Stretch
        });
    }

    private string CopyResponse(string response)
    {
        try
        {
            var package = new DataPackage();
            package.SetText(response);
            Clipboard.SetContent(package);
            return T("copied");
        }
        catch (COMException)
        {
            return T("copyError");
        }
    }

    private ContentControl CreateDocumentPanel(Draft draft)
    {
        var panel = new StackPanel { Spacing = 10 };
        var files = new StackPanel { Spacing = 8 };
        var status = Text(draft.DocumentStatus.Length > 0 ? T(draft.DocumentStatus) : "", 12);
        AutomationProperties.SetLiveSetting(status, AutomationLiveSetting.Polite);
        void SetStatus(string code)
        {
            draft.DocumentStatus = code;
            status.Text = code.Length > 0 ? T(code) : "";
            FrameworkElementAutomationPeer.FromElement(status)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        }
        void RenderFiles()
        {
            files.Children.Clear();
            foreach (var document in draft.Documents.ToArray())
            {
                var row = new StackPanel { Spacing = 8 };
                row.Children.Add(Text(document.Name));
                if (document.Truncated) row.Children.Add(Text(T("documentPartial"), 12));
                var text = Text(document.Text, 12);
                text.IsTextSelectionEnabled = true;
                row.Children.Add(new Expander
                {
                    Header = T("documentPreview"),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Content = new ScrollViewer { Content = text, MaxHeight = 240 }
                });
                var remove = Button(T("documentRemove"), (_, _) =>
                {
                    if (documentReading is not null) return;
                    draft.Documents.Remove(document);
                    SetStatus("");
                    RenderFiles();
                    panel.Children.OfType<Button>().First().Focus(FocusState.Programmatic);
                });
                AutomationProperties.SetName(remove, $"{T("documentRemove")} {document.Name}");
                row.Children.Add(remove);
                files.Children.Add(row);
            }
            if (draft.Documents.Count > 0) files.Children.Add(Text(T("documentPrivacy"), 12));
        }
        var add = Button(T("attachDocuments"), async (_, _) =>
        {
            if (generation is not null || documentReading is not null) { SetStatus("document-busy"); return; }
            using var controller = new CancellationTokenSource();
            documentReading = controller;
            try
            {
                var picker = new FileOpenPicker();
                WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
                foreach (var extension in DocumentText.Extensions) picker.FileTypeFilter.Add(extension);
                var selection = await picker.PickMultipleFilesAsync();
                controller.Token.ThrowIfCancellationRequested();
                if (selection.Count == 0) return;
                if (selection.Count + draft.Documents.Count > DocumentText.MaxFiles)
                    throw new InferenceException("document-limit");
                SetStatus("documentReading");
                foreach (var file in selection)
                {
                    controller.Token.ThrowIfCancellationRequested();
                    using var stream = await file.OpenStreamForReadAsync();
                    if (stream.Length > DocumentText.MaxBytes) throw new InferenceException("document-size");
                    var bytes = new byte[(int)stream.Length];
                    await stream.ReadExactlyAsync(bytes, controller.Token);
                    var document = await Task.Run(() => DocumentText.Read(file.Name, bytes, controller.Token), controller.Token);
                    controller.Token.ThrowIfCancellationRequested();
                    DocumentText.Validate([.. draft.Documents, document]);
                    draft.Documents.Add(document);
                    RenderFiles();
                }
                SetStatus("documentReady");
            }
            catch (OperationCanceledException) { SetStatus("documentStopped"); }
            catch (InferenceException error) { SetStatus(error.Message); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or COMException)
            { SetStatus("document-unreadable"); }
            finally { documentReading = null; }
        });
        panel.Children.Add(add);
        panel.Children.Add(Text(T("documentHelp"), 12));
        panel.Children.Add(files);
        panel.Children.Add(status);
        RenderFiles();
        return new ContentControl { Content = panel, HorizontalContentAlignment = HorizontalAlignment.Stretch, IsTabStop = false };
    }

    private void ShowPreferences()
    {
        PageBody.Children.Add(Heading(T("preferences"), true));
        PageBody.Children.Add(Text(T("preferencesIntro")));
        AddPreference("language", "languageHelp", ["Français", "English"], language == "fr" ? 0 : 1, value =>
        {
            CancelGeneration();
            language = value == 0 ? "fr" : "en";
            Refresh();
            LanguagePicker.Focus(FocusState.Programmatic);
        });
        AddPreference("density", "densityHelp", [T("comfortable"), T("compact")], compact ? 1 : 0, value =>
        {
            compact = value == 1;
            PageBody.Spacing = compact ? 14 : 24;
        });
        AddPreference("response", "responseHelp", [T("steps"), T("summary")], summary ? 1 : 0,
            value => summary = value == 1);
        PageBody.Children.Add(Text(T("nativePreferences"), 12));
    }

    private void AddPreference(string label, string help, string[] options, int selected, Action<int> changed)
    {
        var select = new ComboBox
        {
            Header = T(label),
            ItemsSource = options,
            SelectedIndex = selected,
            MinWidth = 180,
            MinHeight = 44
        };
        AutomationProperties.SetName(select, T(label));
        AutomationProperties.SetHelpText(select, T(help));
        select.SelectionChanged += (_, _) =>
        {
            if (!updating) changed(select.SelectedIndex);
        };
        PageBody.Children.Add(select);
        PageBody.Children.Add(Text(T(help), 13));
    }

    private static string Normalize(string value)
    {
        return string.Concat(value.Normalize(NormalizationForm.FormD)
            .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark))
            .ToLowerInvariant();
    }

    private async void OpenCommandClicked(object sender, RoutedEventArgs args)
    {
        if (palette is not null) return;
        var search = new TextBox { PlaceholderText = T("commandHint") };
        AutomationProperties.SetName(search, T("search"));
        var results = new ListView
        {
            SelectionMode = ListViewSelectionMode.Single,
            IsItemClickEnabled = true,
            MaxHeight = 340
        };
        AutomationProperties.SetName(results, T("search"));
        var empty = Text(T("noResults"));
        var content = new StackPanel { Spacing = 16 };
        content.Children.Add(search);
        content.Children.Add(results);
        content.Children.Add(empty);
        content.Children.Add(Text(T("commandHelp"), 12));
        var dialog = new ContentDialog
        {
            XamlRoot = Root.XamlRoot,
            Title = T("command"),
            Content = content,
            CloseButtonText = T("close")
        };
        palette = dialog;
        string? selectedAction = null;
        string? selectedContext = null;
        void Choose(ListViewItem item)
        {
            if (item.Tag is not Command command) return;
            selectedAction = command.Action;
            selectedContext = command.Context;
            dialog.Hide();
        }
        void Filter()
        {
            results.Items.Clear();
            var commands = Current.Actions.Select(action =>
                    new Command(action.Title, action.Description, action.Id, null, T("action")))
                .Concat(Current.Contexts.Select(context =>
                    new Command(context.Title, context.Description, context.Action, context.Id, T("context"))));
            var terms = Normalize(search.Text).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var command in commands.Where(command =>
                terms.All(term => Normalize($"{command.Title} {command.Description}").Contains(term))))
            {
                var item = new ListViewItem
                {
                    Content = Text($"{command.Title}  ·  {command.Kind}"),
                    Tag = command,
                    MinHeight = 44
                };
                AutomationProperties.SetName(item, $"{command.Title}, {command.Kind}");
                results.Items.Add(item);
            }
            empty.Visibility = results.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (results.Items.Count > 0) results.SelectedIndex = 0;
        }
        Filter();
        search.TextChanged += (_, _) => Filter();
        search.KeyDown += (_, key) =>
        {
            if (results.Items.Count == 0) return;
            if (key.Key == VirtualKey.Enter && results.SelectedItem is ListViewItem item)
            {
                Choose(item);
                key.Handled = true;
            }
            else if (key.Key is VirtualKey.Down or VirtualKey.Up)
            {
                results.Focus(FocusState.Keyboard);
                key.Handled = true;
            }
        };
        results.ItemClick += (_, click) =>
        {
            if (click.ClickedItem is ListViewItem item) Choose(item);
        };
        results.KeyDown += (_, key) =>
        {
            if (key.Key == VirtualKey.Enter && results.SelectedItem is ListViewItem item)
            {
                Choose(item);
                key.Handled = true;
            }
        };
        dialog.Opened += (_, _) => search.Focus(FocusState.Programmatic);
        await dialog.ShowAsync();
        palette = null;
        if (selectedAction is not null) Navigate("action", selectedAction, selectedContext);
    }

    private void CommandInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        if (palette is not null) palette.Hide();
        else OpenCommandClicked(CommandButton, new RoutedEventArgs());
    }

    private void LanguageChanged(object sender, SelectionChangedEventArgs args)
    {
        if (updating || LanguagePicker.SelectedItem is not ComboBoxItem item || item.Tag is not string value) return;
        CancelGeneration();
        language = value;
        Refresh();
    }

    private void NavigationChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (updating || args.SelectedItem is not NavigationViewItem item || item.Tag is not string destination) return;
        Navigate(destination);
    }

    private sealed record Command(string Title, string Description, string Action, string? Context, string Kind);

    private sealed class Draft
    {
        public string Prompt { get; set; } = "";
        public string Output { get; set; } = "";
        public string Status { get; set; } = "idle";
        public string[] Rules { get; set; } = [];
        public List<AttachedDocument> Documents { get; } = [];
        public string DocumentStatus { get; set; } = "";
    }
}
