using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using PrintCalc.Core.Entities;

namespace PrintCalc.App.Views;

public partial class QuotePreviewWindow : Window
{
    public ObservableCollection<QuoteLine> Lines { get; } = new();
    public string HeaderText { get; }
    public string DateText { get; }
    public string DueText { get; }
    public string PaymentMethodText { get; }
    public string IbanText { get; }
    public string SwiftText { get; }
    public string VariableSymbolText { get; }
    public string SupplierName { get; }
    public string SupplierAddress { get; }
    public string SupplierCity { get; }
    public string SupplierIco { get; }
    public string SupplierDic { get; }
    public string SupplierEmail { get; }
    public string SupplierPhone { get; }
    public string LogoPath { get; }
    public Visibility LogoVisibility { get; }
    public Visibility LogoAreaVisibility { get; }
    public string CustomerName { get; }
    public string CustomerAddress { get; }
    public string CustomerIco { get; }
    public string CustomerDic { get; }
    public string CustomerContact { get; }
    public string NetText { get; }
    public string VatText { get; }
    public string TotalText { get; }
    public string StyleLabel { get; }
    public bool IsPhoenix => StyleLabel == "Phoenix";
    public bool IsAurora => StyleLabel == "Aurora";
    public bool IsSolaris => StyleLabel == "Solaris";
    public Brush AccentBrush { get; }
    public Brush AccentDarkBrush { get; }

    public QuotePreviewWindow(
        Quote quote,
        string supplierName,
        string supplierAddress,
        string supplierIco,
        string supplierDic,
        string supplierEmail,
        string supplierPhone,
        string paymentMethod,
        string iban,
        string swift,
        string logoPath,
        string visualStyle)
    {
        InitializeComponent();
        StyleLabel = visualStyle;
        HeaderText = $"Číslo cenové nabídky: {quote.Number} - {quote.Title}";
        DateText = $"Datum vystavení: {quote.IssueDate:yyyy-MM-dd}";
        DueText = $"Datum splatnosti: {quote.IssueDate.AddDays(60):yyyy-MM-dd}";
        PaymentMethodText = $"Způsob úhrady: {paymentMethod}";
        IbanText = $"ÚČET - IBAN: {iban}";
        SwiftText = $"SWIFT (BIC): {swift}";
        VariableSymbolText = $"Variabilní symbol: {quote.Number.Replace("/", "")}";

        SupplierName = supplierName;
        SupplierAddress = supplierAddress;
        SupplierCity = "";
        SupplierIco = $"IČO: {supplierIco}";
        SupplierDic = $"DIČ: {supplierDic}";
        SupplierEmail = $"Email: {supplierEmail}";
        SupplierPhone = $"Telefon: {supplierPhone}";

        CustomerName = quote.Customer?.Name ?? $"Zákazník ID {quote.CustomerId}";
        CustomerAddress = $"{quote.Customer?.Street} {quote.Customer?.Zip} {quote.Customer?.City}".Trim();
        CustomerIco = string.IsNullOrWhiteSpace(quote.Customer?.CompanyId) ? "" : $"IČO: {quote.Customer.CompanyId}";
        CustomerDic = string.IsNullOrWhiteSpace(quote.Customer?.VatId) ? "" : $"DIČ: {quote.Customer.VatId}";
        CustomerContact = $"{quote.Customer?.Email} {quote.Customer?.Phone}".Trim();

        var net = quote.Lines.Sum(x => x.LineTotal);
        var vat = Math.Round(net * 0.21m, 2, MidpointRounding.AwayFromZero);
        NetText = $"Celkem bez DPH: {net:0.00} Kč";
        VatText = $"DPH: {vat:0.00} Kč";
        TotalText = $"Celkem spolu: {net + vat:0.00} Kč";
        var accent = visualStyle switch
        {
            "Aurora" => Color.FromRgb(43, 103, 178),
            "Solaris" => Color.FromRgb(70, 70, 70),
            _ => Color.FromRgb(234, 122, 45)
        };
        AccentBrush = new SolidColorBrush(accent);
        AccentDarkBrush = new SolidColorBrush(Color.FromRgb(
            (byte)Math.Max(0, accent.R - 30),
            (byte)Math.Max(0, accent.G - 30),
            (byte)Math.Max(0, accent.B - 30)));
        LogoPath = logoPath;
        var hasLogo = !string.IsNullOrWhiteSpace(logoPath) && File.Exists(logoPath);
        LogoVisibility = hasLogo ? Visibility.Visible : Visibility.Collapsed;
        LogoAreaVisibility = hasLogo ? Visibility.Visible : Visibility.Collapsed;

        foreach (var line in quote.Lines.OrderBy(x => x.Id))
            Lines.Add(line);

        DataContext = this;
    }

    private void Export_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
