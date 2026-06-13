using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PrintCalc.App.Views;

public partial class ModuleOverlayWindow : Window
{
    public static readonly DependencyProperty OverlayContentProperty =
        DependencyProperty.Register(nameof(OverlayContent), typeof(UserControl), typeof(ModuleOverlayWindow), new PropertyMetadata(null));
    public static readonly DependencyProperty HeaderTitleProperty =
        DependencyProperty.Register(nameof(HeaderTitle), typeof(string), typeof(ModuleOverlayWindow), new PropertyMetadata("Editace"));
    public static readonly DependencyProperty SaveButtonTextProperty =
        DependencyProperty.Register(nameof(SaveButtonText), typeof(string), typeof(ModuleOverlayWindow), new PropertyMetadata("Uložit"));
    public static readonly DependencyProperty SaveCommandProperty =
        DependencyProperty.Register(nameof(SaveCommand), typeof(ICommand), typeof(ModuleOverlayWindow), new PropertyMetadata(null));

    public UserControl? OverlayContent
    {
        get => (UserControl?)GetValue(OverlayContentProperty);
        set => SetValue(OverlayContentProperty, value);
    }

    public string HeaderTitle
    {
        get => (string)GetValue(HeaderTitleProperty);
        set => SetValue(HeaderTitleProperty, value);
    }

    public string SaveButtonText
    {
        get => (string)GetValue(SaveButtonTextProperty);
        set => SetValue(SaveButtonTextProperty, value);
    }

    public ICommand? SaveCommand
    {
        get => (ICommand?)GetValue(SaveCommandProperty);
        set => SetValue(SaveCommandProperty, value);
    }

    public ModuleOverlayWindow(string title, UserControl content, ICommand? saveCommand = null, string saveButtonText = "Uložit")
    {
        InitializeComponent();
        Title = title;
        HeaderTitle = title;
        OverlayContent = content;
        SaveCommand = saveCommand;
        SaveButtonText = saveButtonText;
    }

    private void Close_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

