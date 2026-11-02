using System.Globalization;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Windows.UI.Core;

namespace Nomi;

public sealed partial class MainWindow
{
    private readonly ChatStore chat = LoadChat();
    private CancellationTokenSource? chatting;
    private string? chatFailed;
    private bool chatNeedsModel;
    private bool chatHidden;
    private bool chatWired;

    private static ChatStore LoadChat()
    {
        var store = new ChatStore();
        store.Load();
        return store;
    }

    private ChatContext ChatContextNow() => Conversation.Context(chatHidden ? [] : board.Tasks, Now, profile.Day,
        id => Current.Contexts.FirstOrDefault(item => item.Id == id)?.Title);

    private void OpenChat(string? text = null)
    {
        CloseSheet();
        Show("conversation");
        if (text is { Length: > 0 }) _ = SendChat(text);
        else DispatcherQueue.TryEnqueue(() => ChatBox.Focus(FocusState.Programmatic));
    }

    private void OpenChatClicked(object sender, RoutedEventArgs args) => OpenChat();

    private void WireChat()
    {
        if (chatWired) return;
        chatWired = true;
        ChatBox.PreviewKeyDown += async (_, args) =>
        {
            if (args.Key != VirtualKey.Enter) return;
            var shift = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);
            if (shift) return;
            args.Handled = true;
            await SendFromBox();
        };
        ChatSend.Click += async (_, _) => await SendFromBox();
        ChatBox.TextChanged += (_, _) => ChatSend.IsEnabled = chatting is null && ChatBox.Text.Trim().Length > 0;
    }

    private async Task SendFromBox()
    {
        var text = ChatBox.Text.Trim();
        if (text.Length == 0 || chatting is not null) return;
        ChatBox.Text = "";
        await SendChat(text);
    }

    private void BuildConversation()
    {
        WireChat();
        ConversationHead.Children.Clear();
        ConversationList.Children.Clear();
        var title = Text(T("conversation"), 24, "NomiInk");
        title.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        AutomationProperties.SetHeadingLevel(title, AutomationHeadingLevel.Level1);
        ConversationHead.Children.Add(title);
        var context = ChatContextNow();
        var bar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var sees = Text(chatHidden ? T("chatHidden") : context.Count == 0 ? T("chatSeesNone")
            : context.Count == 1 ? T("chatSeesOne") : string.Format(CultureInfo.CurrentCulture, T("chatSees"), context.Count), 12.5, "NomiInk2");
        sees.VerticalAlignment = VerticalAlignment.Center;
        AutomationProperties.SetAutomationId(sees, "ChatContext");
        bar.Children.Add(sees);
        var toggle = Chip(chatHidden ? T("chatShow") : T("chatHide"), "NomiGhost", chatHidden ? "\uE7B3" : "\uED1A", "ChatContextToggle");
        toggle.Click += (_, _) => { chatHidden = !chatHidden; BuildConversation(); };
        bar.Children.Add(toggle);
        var fresh = Chip(T("chatNew"), "NomiGhost", "\uE710", "ChatNew");
        fresh.IsEnabled = chat.Items.Count > 0 && chatting is null;
        fresh.Click += (_, _) =>
        {
            chat.Clear();
            chatFailed = null;
            BuildConversation();
            Announce(ChatStatus, T("chatCleared"));
            ChatBox.Focus(FocusState.Programmatic);
        };
        bar.Children.Add(fresh);
        ConversationHead.Children.Add(bar);

        if (chat.Items.Count == 0 && chatting is null)
        {
            ConversationList.Children.Add(Text(T("chatEmpty"), 14, "NomiInk2"));
            var examples = new StackPanel { Spacing = 6 };
            foreach (var key in new[] { "chatExample1", "chatExample2", "chatExample3" })
            {
                var example = Chip(T(key), "NomiChip", "\uE8BD", $"Chat_{key}");
                var text = T(key);
                example.Click += async (_, _) => await SendChat(text);
                examples.Children.Add(example);
            }
            ConversationList.Children.Add(examples);
        }
        foreach (var entry in chat.Items) ConversationList.Children.Add(ChatBubble(entry));
        if (chatting is not null)
        {
            var thinking = new StackPanel { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Left, MaxWidth = 560 };
            thinking.Children.Add(new ProgressBar { IsIndeterminate = true, Height = 3, MinHeight = 3, Width = 220 });
            thinking.Children.Add(Text(T("chatThinking"), 12.5, "NomiInk2"));
            var stop = Chip(T("stop"), "NomiGhost", "\uE71A", "ChatStop");
            stop.Click += (_, _) => chatting?.Cancel();
            thinking.Children.Add(stop);
            ConversationList.Children.Add(thinking);
        }
        else if (chatNeedsModel)
        {
            ConversationList.Children.Add(Text(T("chatNeedsModel"), 13, "NomiInk2"));
            var open = Chip(T("chatOpenAssistant"), "NomiChip", "\uE896", "ChatOpenAssistant");
            open.Click += (_, _) => { chatNeedsModel = false; Show("workspace"); };
            ConversationList.Children.Add(open);
        }
        else if (chatFailed is { } failed)
        {
            ConversationList.Children.Add(Text(T("chatError"), 13, "NomiInk2"));
            var retry = Chip(T("chatRetry"), "NomiChip", "\uE72C", "ChatRetry");
            retry.Click += async (_, _) => await SendChat(failed, true);
            ConversationList.Children.Add(retry);
        }
        ChatBox.PlaceholderText = T("chatPlaceholder");
        ChatBox.IsEnabled = chatting is null;
        AutomationProperties.SetName(ChatBox, T("chatAsk"));
        AutomationProperties.SetName(ChatSend, T("chatSend"));
        ToolTipService.SetToolTip(ChatSend, T("chatSend"));
        ChatSend.IsEnabled = chatting is null && ChatBox.Text.Trim().Length > 0;
        DispatcherQueue.TryEnqueue(() =>
        {
            ConversationScroll.UpdateLayout();
            ConversationScroll.ChangeView(null, ConversationScroll.ScrollableHeight, null, true);
        });
    }

    private UIElement ChatBubble(ChatEntry entry)
    {
        var mine = entry.Role == "user";
        var stack = new StackPanel { Spacing = 6 };
        var body = Text(entry.Text, 14);
        body.IsTextSelectionEnabled = true;
        stack.Children.Add(body);
        foreach (var action in entry.Actions)
        {
            var task = action.TaskId is null ? null : board.Find(action.TaskId);
            var gone = action.TaskId is not null && (task is null || task.Column == Columns.Done && action.Kind != ChatActions.Create);
            var label = string.Format(CultureInfo.CurrentCulture, T(action.Kind switch
            {
                ChatActions.Postpone => "chatActPostpone",
                ChatActions.Split => "chatActSplit",
                ChatActions.Focus => "chatActFocus",
                _ => "chatActCreate"
            }), task?.Title ?? action.Title);
            var button = Chip(action.Done ? $"{label} · {T("chatActDone")}" : label, "NomiChip",
                action.Kind switch { ChatActions.Postpone => "\uE916", ChatActions.Split => "\uE8C6", ChatActions.Focus => "\uE7B3", _ => "\uE710" },
                $"ChatAction_{action.Kind}");
            button.IsEnabled = !action.Done && !gone && chatting is null;
            if (gone && !action.Done) ToolTipService.SetToolTip(button, T("chatActGone"));
            button.Click += async (_, _) => await ApplyChatAction(action);
            stack.Children.Add(button);
        }
        var bubble = new Border
        {
            Child = stack,
            Padding = new Thickness(14, 10, 14, 10),
            CornerRadius = new CornerRadius(16),
            MaxWidth = 620,
            HorizontalAlignment = mine ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            Background = Palette(mine ? "NomiAccentSoft" : "NomiGlassStrong"),
            BorderBrush = Palette("NomiGlassEdge"),
            BorderThickness = new Thickness(mine ? 0 : 1)
        };
        AutomationProperties.SetName(bubble, $"{(mine ? T("chatYou") : "Nomi")} : {entry.Text}");
        AutomationProperties.SetAutomationId(bubble, mine ? "ChatUser" : "ChatNomi");
        return bubble;
    }

    private async Task SendChat(string question, bool retry = false)
    {
        question = question.Trim();
        if (question.Length == 0 || chatting is not null) return;
        if (view != "conversation") Show("conversation");
        chatFailed = null;
        chatNeedsModel = false;
        if (!retry) chat.Add(new ChatEntry { Role = "user", Text = question[..Math.Min(question.Length, Conversation.QuestionLimit)], Time = Now });
        if (!inference.Ready)
        {
            if (!inference.HasModel)
            {
                chatNeedsModel = true;
                BuildConversation();
                Announce(ChatStatus, T("chatNeedsModel"));
                return;
            }
            await PrepareModel();
            if (!inference.Ready)
            {
                chatFailed = question;
                BuildConversation();
                Announce(ChatStatus, T("chatError"));
                return;
            }
        }
        using var controller = new CancellationTokenSource();
        chatting = controller;
        BuildConversation();
        Announce(ChatStatus, T("chatThinking"));
        var earlier = chat.Items.Take(Math.Max(0, chat.Items.Count - 1)).ToArray();
        var context = ChatContextNow();
        try
        {
            var result = await inference.GenerateNomiAsync(Conversation.Prompt(context, earlier, question), "chat", language, "summary",
                controller.Token, instruction: $"{Conversation.Instruction()} {AdaptiveInstruction()}".Trim());
            var (text, actions) = result.Accepted ? Conversation.Read(result.Text, context, board.Tasks) : ("", []);
            if (text.Length == 0 && actions.Count == 0) chatFailed = question;
            else
            {
                chat.Add(new ChatEntry { Role = "nomi", Text = text, Actions = actions, Time = Now });
                Announce(ChatStatus, string.Format(CultureInfo.CurrentCulture, T("chatReplied"), text));
            }
        }
        catch (OperationCanceledException) { chatFailed = question; }
        catch (Exception error) when (error is InferenceException or HttpRequestException or IOException or System.Text.Json.JsonException)
        {
            chatFailed = question;
        }
        finally
        {
            chatting = null;
            if (chatFailed is not null) Announce(ChatStatus, T("chatError"));
            if (view == "conversation") BuildConversation();
        }
    }

    private async Task ApplyChatAction(ChatAction action)
    {
        if (action.Done) return;
        var task = action.TaskId is null ? null : board.Find(action.TaskId);
        if (action.Kind != ChatActions.Create && task is not { Column: not Columns.Done })
        {
            ShowToast(T("chatActGone"), null, null);
            BuildConversation();
            return;
        }
        switch (action.Kind)
        {
            case ChatActions.Create:
                var created = board.Add(ReadPhrase(action.Title), Columns.Todo, null, "chat");
                Finish(TaskAddedText(created), () => board.Delete(created));
                break;
            case ChatActions.Postpone:
                var due = task!.Due;
                var running = task.RunningSince is not null;
                if (running) board.Pause(task, Now);
                task.Due = profile.Day.NextStart(Now);
                board.Save();
                Finish(string.Format(CultureInfo.CurrentCulture, T("chatPostponed"), task.Title), () =>
                {
                    task.Due = due;
                    if (running && board.Find(task.Id) is { } back) board.Start(back, Now);
                    board.Save();
                });
                break;
            case ChatActions.Split:
                action.Done = true;
                chat.Save();
                OpenSheet(task!.Id);
                await SplitTask(task.Id);
                break;
            case ChatActions.Focus:
                action.Done = true;
                chat.Save();
                focusTaskId = task!.Id;
                Show("focus");
                break;
        }

        void Finish(string message, System.Action undo)
        {
            action.Done = true;
            chat.Save();
            BuildConversation();
            BuildCompanion();
            ShowToast(message, T("undo"), () =>
            {
                undo();
                action.Done = false;
                chat.Save();
                Refresh();
                Announce(ChatStatus, T("chatUndone"));
            });
        }
    }
}
