using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using PrintCalc.Core.Entities;

namespace PrintCalc.App.Views;

public partial class InvoicePreviewWindow : Window
{
    public ObservableCollection<InvoiceLine> Lines { get; } = new();
    public string HeaderText { get; }
    public string DateText { get; }
    public string SubHeaderText { get; }
    public string IntroText { get; }
    public string DueText { get; }
    public string TaxDateText { get; }
    public string PaymentMethodText { get; }
    public string VariableSymbolText { get; }
    public string IbanText { get; }
    public string SwiftText { get; }
    public string BankAccountText { get; }
    public string SupplierText { get; }
    public string CustomerText { get; }
    public string NetText { get; }
    public string VatText { get; }
    public string TotalText { get; }
    public string TotalAmountBig { get; }
    public string SummaryTitle { get; }
    public string StyleLabel { get; }
    public bool IsPhoenix => StyleLabel == "Phoenix";
    public bool IsAurora => StyleLabel == "Aurora";
    public bool IsSolaris => StyleLabel == "Solaris";
    public Brush AccentBrush { get; }
    public Brush AccentSoftBrush { get; }
    public Brush AccentDarkBrush { get; }
    public string LogoPath { get; }
    public Visibility LogoVisibility { get; }
    /// <summary>Celý rámeček loga (včetně prázdného okraje) — skrytý bez souboru loga.</summary>
    public Visibility LogoAreaVisibility { get; }
    public string ConfirmButtonText { get; }
    public Visibility ConfirmButtonVisibility { get; }

    public InvoicePreviewWindow(
        Invoice invoice,
        string supplierName,
        string supplierAddress,
        string supplierIco,
        string supplierDic,
        string supplierEmail,
        string supplierPhone,
        string paymentMethod,
        string iban,
        string swift,
        string bankAccount,
        string logoPath,
        string visualStyle,
        string customerName,
        string customerAddress,
        string customerContact,
        string customerIco,
        string customerDic,
        bool showConfirmButton = true,
        string confirmButtonText = "Uložit fakturu")
    {
        InitializeComponent();
        StyleLabel = visualStyle;
        HeaderText = visualStyle switch
        {
            "Aurora" => $"Faktura {invoice.Number}",
            "Solaris" => $"DAŇOVÝ DOKLAD {invoice.Number}",
            _ => $"FAKTURA {invoice.Number}"
        };
        SubHeaderText = visualStyle switch
        {
            "Phoenix" => "DAŇOVÝ DOKLAD",
            "Aurora" => "Obsah",
            _ => "DAŇOVÝ DOKLAD"
        };
        IntroText = visualStyle switch
        {
            "Phoenix" => $"Prosím o zaplacení částky {invoice.TotalAmount:0.00} Kč",
            "Aurora" => "Fakturujeme Vám následující položky",
            _ => "Fakturujeme Vám následující položky"
        };
        DateText = $"Datum vystavení: {invoice.IssueDate:dd. MM. yyyy}";
        DueText = $"Datum splatnosti: {(invoice.DueDate ?? invoice.IssueDate.AddDays(14)):dd. MM. yyyy}";
        TaxDateText = $"Datum zdan. plnění: {invoice.IssueDate:dd. MM. yyyy}";
        PaymentMethodText = $"Způsob platby: {paymentMethod}";
        VariableSymbolText = $"Variabilní symbol: {invoice.Number.Replace("/", "")}";
        IbanText = $"IBAN: {iban}";
        SwiftText = $"SWIFT (BIC): {swift}";
        BankAccountText = $"Bankovní účet: {bankAccount}";
        SupplierText = $"{supplierName}\n{supplierAddress}\nIČO: {supplierIco}   DIČ: {supplierDic}\n{supplierEmail}   {supplierPhone}".Trim();
        var customerIcoLine = string.IsNullOrWhiteSpace(customerIco) ? string.Empty : $"IČO: {customerIco}";
        var customerDicLine = string.IsNullOrWhiteSpace(customerDic) ? string.Empty : $"DIČ: {customerDic}";
        CustomerText = $"{customerName}\n{customerAddress}\n{customerIcoLine}\n{customerDicLine}\n{customerContact}".Trim();

        var net = invoice.Lines.Sum(x => x.LineTotal);
        var vat = Math.Round(invoice.Lines.Sum(x => x.LineTotal * (x.TaxRatePercent / 100m)), 2, MidpointRounding.AwayFromZero);
        var total = net + vat;
        NetText = $"Základ: {net:0.00} Kč";
        VatText = $"DPH: {vat:0.00} Kč";
        TotalText = $"Celkem k úhradě: {total:0.00} Kč";
        TotalAmountBig = $"{total:0.00} Kč";
        SummaryTitle = visualStyle == "Aurora" ? "Sumář" : "Rekapitulace";

        var accent = visualStyle switch
        {
            "Aurora" => Color.FromRgb(43, 103, 178),
            "Solaris" => Color.FromRgb(70, 70, 70),
            _ => Color.FromRgb(234, 122, 45)
        };
        AccentBrush = new SolidColorBrush(accent);
        AccentSoftBrush = new SolidColorBrush(Color.FromArgb(30, accent.R, accent.G, accent.B));
        AccentDarkBrush = new SolidColorBrush(Color.FromRgb(
            (byte)Math.Max(0, accent.R - 20),
            (byte)Math.Max(0, accent.G - 20),
            (byte)Math.Max(0, accent.B - 20)));

        LogoPath = logoPath;
        var hasLogo = !string.IsNullOrWhiteSpace(logoPath) && File.Exists(logoPath);
        LogoVisibility = hasLogo ? Visibility.Visible : Visibility.Collapsed;
        LogoAreaVisibility = hasLogo ? Visibility.Visible : Visibility.Collapsed;
        ConfirmButtonText = string.IsNullOrWhiteSpace(confirmButtonText) ? "Uložit fakturu" : confirmButtonText;
        ConfirmButtonVisibility = showConfirmButton ? Visibility.Visible : Visibility.Collapsed;

        foreach (var line in invoice.Lines.OrderBy(x => x.Id))
            Lines.Add(line);
        DataContext = this;
    }

    private void Export_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
