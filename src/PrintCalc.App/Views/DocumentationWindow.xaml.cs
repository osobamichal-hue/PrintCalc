using System.Windows;

namespace PrintCalc.App.Views;

public partial class DocumentationWindow : Window
{
    public static readonly DependencyProperty DocumentationTextProperty =
        DependencyProperty.Register(nameof(DocumentationText), typeof(string), typeof(DocumentationWindow), new PropertyMetadata(string.Empty));

    public string DocumentationText
    {
        get => (string)GetValue(DocumentationTextProperty);
        set => SetValue(DocumentationTextProperty, value);
    }

    public DocumentationWindow()
    {
        InitializeComponent();
    }
}
