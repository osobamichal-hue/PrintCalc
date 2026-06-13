using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using PrintCalc.App.Services;
using PrintCalc.Core.Entities;

namespace PrintCalc.App.Views;

public partial class NewQuoteFromCalculationsWindow : Window, INotifyPropertyChanged
{
    public ObservableCollection<CalculationPickItem> Items { get; } = new();
    public IReadOnlyList<int> SelectedCalculationIds => Items.Where(x => x.IsSelected).Select(x => x.Id).ToList();
    public string SelectedCountText => $"Vybráno: {Items.Count(x => x.IsSelected)}";
    public bool CreateAsDetailedCalculation { get; set; } = true;

    public NewQuoteFromCalculationsWindow(IEnumerable<Calculation> calculations)
    {
        InitializeComponent();
        foreach (var c in calculations.OrderByDescending(x => x.CreatedAt))
        {
            var item = new CalculationPickItem
            {
                Id = c.Id,
                CreatedAt = c.CreatedAt,
                Title = c.Title,
                Total = c.TotalWithMargin,
                ModeLabel = c.ModeLabel
            };
            item.PropertyChanged += OnItemPropertyChanged;
            Items.Add(item);
        }
        MaxWidth = SystemParameters.WorkArea.Width * 0.9;
        MaxHeight = SystemParameters.WorkArea.Height * 0.9;
        DataContext = this;
    }

    private void Create_OnClick(object sender, RoutedEventArgs e)
    {
        if (CalcGrid.SelectedItems.Count > 0)
        {
            foreach (var selected in CalcGrid.SelectedItems.OfType<CalculationPickItem>())
                selected.IsSelected = true;
        }

        if (SelectedCalculationIds.Count == 0)
        {
            AppDialog.ShowInfo("Vyberte alespoň jednu kalkulaci.", "Nová nabídka");
            return;
        }

        DialogResult = true;
        Close();
    }

    private void SelectAll_OnClick(object sender, RoutedEventArgs e)
    {
        foreach (var i in Items) i.IsSelected = true;
        CalcGrid.SelectAll();
        NotifySelectedCountChanged();
    }

    private void ClearSelection_OnClick(object sender, RoutedEventArgs e)
    {
        foreach (var i in Items) i.IsSelected = false;
        CalcGrid.SelectedItems.Clear();
        NotifySelectedCountChanged();
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CalculationPickItem.IsSelected))
            NotifySelectedCountChanged();
    }

    private void NotifySelectedCountChanged() =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedCountText)));

    public event PropertyChangedEventHandler? PropertyChanged;
}

public class CalculationPickItem : INotifyPropertyChanged
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Title { get; set; } = "";
    public string ModeLabel { get; set; } = "";
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
