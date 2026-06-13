using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using PrintCalc.Core.Entities;

namespace PrintCalc.App.Views;

public partial class OrderPreviewWindow : Window
{
    public ObservableCollection<OrderLine> Lines { get; } = new();
    public string HeaderText { get; }
    public string DateText { get; }
    public string TotalText { get; }
    public string CustomerText { get; }

    public OrderPreviewWindow(Order order, string customerName, string customerAddress, string customerIco, string customerDic)
    {
        InitializeComponent();
        HeaderText = $"Zakazka {order.Number} - {order.Title}";
        DateText = $"Datum: {order.CreatedAt:yyyy-MM-dd}";
        TotalText = $"Celkem: {order.TotalAmount:0.00} Kč";
        var ico = string.IsNullOrWhiteSpace(customerIco) ? "" : $"IČO: {customerIco}";
        var dic = string.IsNullOrWhiteSpace(customerDic) ? "" : $"DIČ: {customerDic}";
        CustomerText = $"{customerName}\n{customerAddress}\n{ico}\n{dic}".Trim();
        foreach (var line in order.Lines.OrderBy(x => x.Id))
            Lines.Add(line);
        DataContext = this;
    }

    private void Confirm_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
