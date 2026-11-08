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
    private bool chatHidden;

    private ChatContext ChatContextNow() => Conversation.Context(chatHidden ? [] : board.Tasks, Now, profile.Day,
        id => Current.Contexts.FirstOrDefault(item => item.Id == id)?.Title);

    private void OpenChatClicked(object sender, RoutedEventArgs args) => OpenNomi();

    private async Task ApplyChatAction(ChatAction action)
    {
        if (action.Done) return;
        var task = action.TaskId is null ? null : board.Find(action.TaskId);
        if (action.Kind != ChatActions.Create && task is not { Column: not Columns.Done })
        {
            ShowToast(T("chatActGone"), null, null);
            if (view == "nomi") BuildNomi();
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
                threads.Save();
                OpenSheet(task!.Id);
                await SplitTask(task.Id);
                break;
            case ChatActions.Focus:
                action.Done = true;
                threads.Save();
                focusTaskId = task!.Id;
                Show("focus");
                break;
        }

        void Finish(string message, System.Action undo)
        {
            action.Done = true;
            threads.Save();
            if (view == "nomi") BuildNomi();
            BuildCompanion();
            ShowToast(message, T("undo"), () =>
            {
                undo();
                action.Done = false;
                threads.Save();
                Refresh();
                AnnounceNomi(T("chatUndone"));
            });
        }
    }
}
