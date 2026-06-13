using System.Windows;
using PrintCalc.App.Views;

namespace PrintCalc.App.Services;

public static class AppDialog
{
    public static MessageBoxResult ShowInfo(string message, string title = "PrintCalc") =>
        Show(message, title, "Informace", MessageBoxButton.OK);

    public static MessageBoxResult ShowError(string message, string title = "PrintCalc") =>
        Show(message, title, "Chyba", MessageBoxButton.OK);

    public static MessageBoxResult ShowQuestion(string message, string title = "PrintCalc", MessageBoxButton buttons = MessageBoxButton.YesNo) =>
        Show(message, title, "Dotaz", buttons);

    private static MessageBoxResult Show(string message, string title, string tone, MessageBoxButton buttons)
    {
        var owner = Application.Current?.MainWindow;
        var dlg = new AppDialogWindow
        {
            Owner = owner,
            TitleText = title,
            ToneText = tone,
            MessageText = message,
            Buttons = buttons
        };
        var accepted = dlg.ShowDialog() == true;

        return buttons switch
        {
            MessageBoxButton.OK => MessageBoxResult.OK,
            MessageBoxButton.OKCancel => accepted ? MessageBoxResult.OK : MessageBoxResult.Cancel,
            MessageBoxButton.YesNo => accepted ? MessageBoxResult.Yes : MessageBoxResult.No,
            MessageBoxButton.YesNoCancel => dlg.CancelClicked
                ? MessageBoxResult.Cancel
                : (accepted ? MessageBoxResult.Yes : MessageBoxResult.No),
            _ => MessageBoxResult.None
        };
    }
}

