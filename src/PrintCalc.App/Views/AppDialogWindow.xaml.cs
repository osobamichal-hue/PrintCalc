using System.Windows;

namespace PrintCalc.App.Views;

public partial class AppDialogWindow : Window
{
    public static readonly DependencyProperty TitleTextProperty =
        DependencyProperty.Register(nameof(TitleText), typeof(string), typeof(AppDialogWindow), new PropertyMetadata("PrintCalc"));
    public static readonly DependencyProperty MessageTextProperty =
        DependencyProperty.Register(nameof(MessageText), typeof(string), typeof(AppDialogWindow), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty ToneTextProperty =
        DependencyProperty.Register(nameof(ToneText), typeof(string), typeof(AppDialogWindow), new PropertyMetadata("Informace"));
    public static readonly DependencyProperty ButtonsProperty =
        DependencyProperty.Register(nameof(Buttons), typeof(MessageBoxButton), typeof(AppDialogWindow), new PropertyMetadata(MessageBoxButton.OK, OnButtonsChanged));

    public string TitleText
    {
        get => (string)GetValue(TitleTextProperty);
        set => SetValue(TitleTextProperty, value);
    }

    public string MessageText
    {
        get => (string)GetValue(MessageTextProperty);
        set => SetValue(MessageTextProperty, value);
    }

    public string ToneText
    {
        get => (string)GetValue(ToneTextProperty);
        set => SetValue(ToneTextProperty, value);
    }

    public MessageBoxButton Buttons
    {
        get => (MessageBoxButton)GetValue(ButtonsProperty);
        set => SetValue(ButtonsProperty, value);
    }

    public bool CancelClicked { get; private set; }

    public AppDialogWindow()
    {
        InitializeComponent();
        UpdateButtons();
    }

    private static void OnButtonsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AppDialogWindow w)
            w.UpdateButtons();
    }

    private void UpdateButtons()
    {
        switch (Buttons)
        {
            case MessageBoxButton.OK:
                YesOkButton.Content = "OK";
                YesOkButton.Visibility = Visibility.Visible;
                NoButton.Visibility = Visibility.Collapsed;
                CancelButtonEx.Visibility = Visibility.Collapsed;
                break;
            case MessageBoxButton.OKCancel:
                YesOkButton.Content = "OK";
                YesOkButton.Visibility = Visibility.Visible;
                NoButton.Visibility = Visibility.Collapsed;
                CancelButtonEx.Visibility = Visibility.Visible;
                break;
            case MessageBoxButton.YesNo:
                YesOkButton.Content = "Ano";
                YesOkButton.Visibility = Visibility.Visible;
                NoButton.Visibility = Visibility.Visible;
                CancelButtonEx.Visibility = Visibility.Collapsed;
                break;
            case MessageBoxButton.YesNoCancel:
                YesOkButton.Content = "Ano";
                YesOkButton.Visibility = Visibility.Visible;
                NoButton.Visibility = Visibility.Visible;
                CancelButtonEx.Visibility = Visibility.Visible;
                break;
        }
    }

    private void YesOkButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void NoButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void CancelButtonEx_Click(object sender, RoutedEventArgs e)
    {
        CancelClicked = true;
        DialogResult = false;
        Close();
    }
}

