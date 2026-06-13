using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using PrintCalc.Core.Helpers;
using PrintCalc.App.Services;
using PrintCalc.Core.Entities;

namespace PrintCalc.App.Views;

public partial class NewOrderFromQuotesWindow : Window, INotifyPropertyChanged
{
    public ObservableCollection<QuotePickItem> Items { get; } = new();
    public IReadOnlyList<int> SelectedQuoteIds => Items.Where(x => x.IsSelected).Select(x => x.Id).ToList();
    public string SelectedCountText => $"Vybráno: {Items.Count(x => x.IsSelected)}";
    public bool CreateAsDetailedFromQuotes { get; set; } = true;
    public event PropertyChangedEventHandler? PropertyChanged;

    public NewOrderFromQuotesWindow(IEnumerable<Quote> quotes)
    {
        InitializeComponent();
        foreach (var q in quotes.OrderByDescending(x => x.IssueDate))
        {
            var item = new QuotePickItem
            {
                Id = q.Id,
                Number = q.Number,
                IssueDate = q.IssueDate,
                Status = q.Status.ToString(),
                Title = DocumentTitleExcerpt.PickerLabelFromQuote(q),
                Total = q.TotalAmount
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
        if (QuoteGrid.SelectedItems.Count > 0)
            foreach (var s in QuoteGrid.SelectedItems.OfType<QuotePickItem>()) s.IsSelected = true;
        if (SelectedQuoteIds.Count == 0)
        {
            AppDialog.ShowInfo("Vyberte alespoň jednu nabídku.", "Nová zakázka");
            return;
        }
        DialogResult = true;
        Close();
    }

    private void SelectAll_OnClick(object sender, RoutedEventArgs e)
    {
        foreach (var i in Items) i.IsSelected = true;
        QuoteGrid.SelectAll();
        NotifyCount();
    }

    private void ClearSelection_OnClick(object sender, RoutedEventArgs e)
    {
        foreach (var i in Items) i.IsSelected = false;
        QuoteGrid.SelectedItems.Clear();
        NotifyCount();
    }

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(QuotePickItem.IsSelected)) NotifyCount();
    }

    private void NotifyCount() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedCountText)));
}

public class QuotePickItem : INotifyPropertyChanged
{
    public int Id { get; set; }
    public string Number { get; set; } = "";
    public DateTime IssueDate { get; set; }
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
