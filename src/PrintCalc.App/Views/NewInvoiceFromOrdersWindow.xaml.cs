using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using PrintCalc.App.Services;
using PrintCalc.Core.Entities;

namespace PrintCalc.App.Views;

public partial class NewInvoiceFromOrdersWindow : Window, INotifyPropertyChanged
{
    public ObservableCollection<OrderPickItem> Items { get; } = new();
    public IReadOnlyList<int> SelectedOrderIds => Items.Where(x => x.IsSelected).Select(x => x.Id).ToList();
    public string SelectedCountText => $"Vybráno: {Items.Count(x => x.IsSelected)}";
    public bool CreateAsDetailedFromOrders { get; set; } = true;
    public event PropertyChangedEventHandler? PropertyChanged;

    public NewInvoiceFromOrdersWindow(IEnumerable<Order> orders)
    {
        InitializeComponent();
        foreach (var o in orders.OrderByDescending(x => x.CreatedAt))
        {
            var item = new OrderPickItem
            {
                Id = o.Id,
                Number = o.Number,
                CreatedAt = o.CreatedAt,
                Status = o.Status.ToString(),
                Title = o.Title,
                Total = o.TotalAmount
            };
            item.PropertyChanged += OnItemChanged;
            Items.Add(item);
        }
        MaxWidth = SystemParameters.WorkArea.Width * 0.9;
        MaxHeight = SystemParameters.WorkArea.Height * 0.9;
        DataContext = this;
    }

    private void Create_OnClick(object sender, RoutedEventArgs e)
    {
        if (OrderGrid.SelectedItems.Count > 0)
            foreach (var s in OrderGrid.SelectedItems.OfType<OrderPickItem>()) s.IsSelected = true;
        if (SelectedOrderIds.Count == 0)
        {
            AppDialog.ShowInfo("Vyberte alespoň jednu zakázku.", "Nová faktura");
            return;
        }
        DialogResult = true;
        Close();
    }

    private void SelectAll_OnClick(object sender, RoutedEventArgs e)
    {
        foreach (var i in Items) i.IsSelected = true;
        OrderGrid.SelectAll();
        NotifyCount();
    }

    private void ClearSelection_OnClick(object sender, RoutedEventArgs e)
    {
        foreach (var i in Items) i.IsSelected = false;
        OrderGrid.SelectedItems.Clear();
        NotifyCount();
    }

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OrderPickItem.IsSelected)) NotifyCount();
    }

    private void NotifyCount() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedCountText)));
}

public class OrderPickItem : INotifyPropertyChanged
{
    public int Id { get; set; }
    public string Number { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; } = "";
    public string Title { get; set; } = "";
    public decimal Total { get; set; }
    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
