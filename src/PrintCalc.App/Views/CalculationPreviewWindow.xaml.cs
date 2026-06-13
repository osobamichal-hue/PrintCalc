using System.Windows;
using PrintCalc.Core.Entities;

namespace PrintCalc.App.Views;

public partial class CalculationPreviewWindow : Window
{
    public string HeaderText { get; }
    public string DateText { get; }
    public string MaterialText { get; }
    public string PrintText { get; }
    public string EnergyText { get; }
    public string PiecesSummaryText { get; }
    public string PrintDurationText { get; }
    public string ModelDesignText { get; }
    public string StartFeeText { get; }
    public string SubtotalText { get; }
    public string TotalText { get; }
    public string ModeText { get; }
    public string ModelText { get; }

    public CalculationPreviewWindow(Calculation calc)
    {
        InitializeComponent();
        HeaderText = $"Kalkulace #{calc.Id} - {calc.Title}";
        DateText = $"Datum: {calc.CreatedAt.ToLocalTime():yyyy-MM-dd HH:mm}";
        var piecesPerBuild = calc.PiecesPerBuild <= 0 ? 1 : calc.PiecesPerBuild;
        var requiredPieces = calc.RequiredPieces <= 0 ? 1 : calc.RequiredPieces;
        var printRuns = calc.PrintRuns <= 0 ? 1 : calc.PrintRuns;
        PiecesSummaryText = $"Kusy/podložka: {piecesPerBuild} | Požadováno: {requiredPieces} | Opakování tisku: {printRuns}x";
        PrintDurationText = $"Doba tisku jedné podložky: {calc.PrintHours:0.##} h";
        MaterialText = $"Materiál: {calc.MaterialCost:0.00} Kč";
        PrintText = $"Tisk: {calc.PrintCost:0.00} Kč";
        EnergyText = $"Energie: {calc.EnergyCost:0.00} Kč";
        ModelDesignText = $"Modelování: {calc.ModelDesignCost:0.00} Kč";
        StartFeeText = $"Pevný poplatek: {calc.StartFeeCost:0.00} Kč";
        SubtotalText = $"Mezisoučet: {calc.Subtotal:0.00} Kč";
        TotalText = $"Celkem: {calc.TotalWithMargin:0.00} Kč";
        ModeText = calc.ModeLabel;
        ModelText = string.IsNullOrWhiteSpace(calc.SourceModelPath) ? "—" : calc.SourceModelPath;
        DataContext = this;
    }

    private void Export_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
