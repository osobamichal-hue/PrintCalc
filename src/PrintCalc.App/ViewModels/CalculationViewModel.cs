using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using PrintCalc.App.Helpers;
using PrintCalc.Core.Helpers;
using PrintCalc.App.Services;
using PrintCalc.App.Views;
using PrintCalc.Core.Entities;
using PrintCalc.Core.Enums;
using PrintCalc.Core.Models;
using PrintCalc.Core.Services;
using PrintCalc.Infrastructure.Persistence;

namespace PrintCalc.App.ViewModels;

public partial class CalculationViewModel : ObservableObject
{
    private readonly AppDbContext _db;
    private readonly ICalculationEngine _engine;
    private readonly IThreeMfReader _threeMf;
    private readonly IGcodeReader _gcode;
    private readonly IModelMetadataResolver _metadataResolver;
    private readonly IStockService _stock;
    private readonly IDocumentNumberService _numbers;
    private readonly ICalculationPdfService _calculationPdf;
    private bool _isLoadingCalculationIntoForm;

    public ObservableCollection<Customer> Customers { get; } = new();
    public ObservableCollection<FilamentType> Filaments { get; } = new();
    public ObservableCollection<Printer> Printers { get; } = new();
    public ObservableCollection<PrintModel> Models { get; } = new();
    public ObservableCollection<Calculation> Calculations { get; } = new();

    [ObservableProperty] private int? selectedCustomerId;
    [ObservableProperty] private int? selectedFilamentTypeId;
    [ObservableProperty] private int? selectedPrinterId;
    [ObservableProperty] private int? selectedModelId;
    [ObservableProperty] private Calculation? selectedCalculation;

    [ObservableProperty] private string filamentSummary = "—";
    [ObservableProperty] private string printerSummary = "—";
    [ObservableProperty] private string modelPathDisplay = "—";
    [ObservableProperty] private string statusMessage = "";

    [ObservableProperty] private decimal materialGrams = 50;
    [ObservableProperty] private int printDurationHours = 2;
    [ObservableProperty] private int printDurationMinutes = 0;
    [ObservableProperty] private int piecesPerBuild = 1;
    [ObservableProperty] private int requiredPieces = 1;
    [ObservableProperty] private int modelDesignDurationHours = 0;
    [ObservableProperty] private int modelDesignDurationMinutes = 0;
    [ObservableProperty] private decimal modelingHourlyRate = 450;
    [ObservableProperty] private bool includeModelDesign = true;
    /// <summary>Materiál dodá zákazník — cena materiálu 0 Kč, poznámka v nabídce a faktuře.</summary>
    [ObservableProperty] private bool customerSuppliedMaterial;
    [ObservableProperty] private decimal marginPercent = 15;
    [ObservableProperty] private decimal slicingFeePerModel = 100;
    [ObservableProperty] private decimal postProcessingHours;
    [ObservableProperty] private decimal postProcessingHourlyRate = 350;
    [ObservableProperty] private decimal wasteCoefficientPercent;
    [ObservableProperty] private decimal filamentPricePerKg;
    [ObservableProperty] private string calculationTitle = "Kalkulace";
    /// <summary>Volitelný popis řádku tisku při převodu do nabídky (prázdné = „název - 3D tisk“).</summary>
    [ObservableProperty] private string quotePrintDescriptionOverride = "";
    [ObservableProperty] private string quotePrintLinePreview = "";
    [ObservableProperty] private DateTime calculationDateTime = DateTime.Now;
    [ObservableProperty] private int calculationTimeHours = DateTime.Now.Hour;
    [ObservableProperty] private int calculationTimeMinutes = DateTime.Now.Minute;

    [ObservableProperty] private decimal resultMaterial;
    [ObservableProperty] private decimal resultPrint;
    [ObservableProperty] private decimal resultEnergy;
    [ObservableProperty] private decimal resultModelDesign;
    [ObservableProperty] private decimal resultStartFee;
    [ObservableProperty] private decimal resultSlicingFee;
    [ObservableProperty] private decimal resultPostProcessing;
    [ObservableProperty] private decimal resultQuantityDiscount;
    [ObservableProperty] private decimal resultQuantityDiscountPercent;
    [ObservableProperty] private decimal resultDiscountedSubtotal;
    [ObservableProperty] private decimal resultSubtotal;
    [ObservableProperty] private decimal resultTotal;
    [ObservableProperty] private int resultPrintRuns;
    [ObservableProperty] private decimal resultUnitPrice;

    /// <summary>Rozpis řádku „strojní čas“ pro zobrazení ve výsledku.</summary>
    [ObservableProperty] private string resultPrintBreakdown = "";
    [ObservableProperty] private string resultEnergyBreakdown = "";
    [ObservableProperty] private string resultStartFeeBreakdown = "";
    [ObservableProperty] private string resultUnitBreakdown = "";
    [ObservableProperty] private bool manualPriceEditingEnabled;

    [ObservableProperty] private string threeMfWarnings = "";
    [ObservableProperty] private int wizardStepIndex = 4;
    [ObservableProperty] private bool compactTables;

    public decimal MaterialGramsPerPieceDisplay =>
        Math.Round(MaterialGrams / Math.Max(1, PiecesPerBuild), 2, MidpointRounding.AwayFromZero);

    public decimal PrintHoursPerPieceDisplay =>
        Math.Round(GetDurationHours() / Math.Max(1, PiecesPerBuild), 3, MidpointRounding.AwayFromZero);
    public double TableRowHeight => CompactTables ? 30 : 42;

    public CalculationViewModel(
        AppDbContext db,
        ICalculationEngine engine,
        IThreeMfReader threeMf,
        IGcodeReader gcode,
        IModelMetadataResolver metadataResolver,
        IStockService stock,
        IDocumentNumberService numbers,
        ICalculationPdfService calculationPdf)
    {
        _db = db;
        _engine = engine;
        _threeMf = threeMf;
        _gcode = gcode;
        _metadataResolver = metadataResolver;
        _stock = stock;
        _numbers = numbers;
        _calculationPdf = calculationPdf;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        Customers.Clear();
        foreach (var c in await _db.Customers.OrderBy(x => x.Name).AsNoTracking().ToListAsync())
            Customers.Add(c);

        Filaments.Clear();
        foreach (var f in await _db.FilamentTypes.OrderBy(x => x.Name).AsNoTracking().ToListAsync())
            Filaments.Add(f);

        Printers.Clear();
        foreach (var p in await _db.Printers.OrderBy(x => x.Name).AsNoTracking().ToListAsync())
            Printers.Add(p);

        Models.Clear();
        foreach (var m in await _db.PrintModels.OrderByDescending(x => x.CreatedAt).AsNoTracking().ToListAsync())
            Models.Add(m);

        ModelingHourlyRate = await GetModelingHourlyRateAsync();
        SlicingFeePerModel = await GetDecimalSettingAsync("Calculation.DefaultSlicingFeePerModel", 100m);
        PostProcessingHourlyRate = await GetDecimalSettingAsync("Calculation.PostProcessingHourlyRate", 350m);
        await LoadCalculationsAsync();
        RefreshQuotePrintLinePreview();
    }

    private async Task LoadCalculationsAsync()
    {
        var query = _db.Calculations.AsNoTracking().OrderByDescending(c => c.CreatedAt).AsQueryable();
        if (SelectedCustomerId is { } cid)
            query = query.Where(c => c.CustomerId == cid);

        var list = await query.Take(200).ToListAsync();
        await DispatcherObservableRefresh.ReplaceAsync(Calculations, list);
    }

    public void ApplyFilamentTypeId(int id) => SelectedFilamentTypeId = id;

    public void ApplyPrinterId(int id) => SelectedPrinterId = id;

    public void ApplyModelFile(string path)
    {
        SelectedModelId = null;
        ModelPathDisplay = path;

        decimal density = 1.24m;
        if (SelectedFilamentTypeId is { } fid)
        {
            var ft = Filaments.FirstOrDefault(f => f.Id == fid);
            if (ft is { DensityGPerCm3: > 0 }) density = ft.DensityGPerCm3;
        }

        var meta = _metadataResolver.Resolve(path, density);
        ThreeMfWarnings = meta.Warnings.Count == 0 ? "" : string.Join("\n", meta.Warnings);
        if (meta.MaterialGrams is { } g) MaterialGrams = g;
        if (meta.PrintHours is { } h) SetDurationFromHours(h);
        StatusMessage = $"Načten model: {System.IO.Path.GetFileName(path)}";
    }

    partial void OnSelectedModelIdChanged(int? value)
    {
        if (_isLoadingCalculationIntoForm) return;
        if (value is null) return;
        var model = Models.FirstOrDefault(x => x.Id == value.Value);
        if (model is null) return;
        ModelPathDisplay = string.IsNullOrWhiteSpace(model.FilePath)
            ? $"Uloženo v DB: {model.OriginalFileName}"
            : $"Uloženo v DB: {model.OriginalFileName} (zdroj: {model.FilePath})";
        if (model.EstimatedMaterialGrams is { } g) MaterialGrams = g;
        if (model.EstimatedPrintHours is { } h) SetDurationFromHours(h);
        if (string.IsNullOrWhiteSpace(CalculationTitle) || CalculationTitle == "Kalkulace")
            CalculationTitle = model.Name;
    }

    partial void OnSelectedFilamentTypeIdChanged(int? value)
    {
        if (value is null)
        {
            FilamentSummary = "—";
            FilamentPricePerKg = 0;
            return;
        }

        var ft = Filaments.FirstOrDefault(f => f.Id == value);
        if (ft is null) return;
        FilamentSummary = $"{ft.Name} ({ft.Manufacturer})";
        FilamentPricePerKg = ft.AveragePricePerKg;
    }

    partial void OnSelectedPrinterIdChanged(int? value)
    {
        if (value is null)
        {
            PrinterSummary = "—";
            return;
        }

        var p = Printers.FirstOrDefault(x => x.Id == value);
        PrinterSummary = p is null ? "—" : $"{p.Name} ({p.Kind})";
    }

    partial void OnSelectedCustomerIdChanged(int? value)
    {
        if (_isLoadingCalculationIntoForm) return;
        _ = LoadCalculationsAsync();
    }

    partial void OnMaterialGramsChanged(decimal value)
    {
        OnPropertyChanged(nameof(MaterialGramsPerPieceDisplay));
    }

    partial void OnPrintDurationHoursChanged(int value)
    {
        OnPropertyChanged(nameof(PrintHoursPerPieceDisplay));
    }

    partial void OnPrintDurationMinutesChanged(int value)
    {
        var normalized = Math.Clamp(value, 0, 59);
        if (value != normalized)
        {
            PrintDurationMinutes = normalized;
            return;
        }
        OnPropertyChanged(nameof(PrintHoursPerPieceDisplay));
    }

    partial void OnPiecesPerBuildChanged(int value)
    {
        OnPropertyChanged(nameof(MaterialGramsPerPieceDisplay));
        OnPropertyChanged(nameof(PrintHoursPerPieceDisplay));
    }

    partial void OnModelDesignDurationMinutesChanged(int value)
    {
        var normalized = Math.Clamp(value, 0, 59);
        if (value != normalized)
            ModelDesignDurationMinutes = normalized;
    }

    partial void OnWizardStepIndexChanged(int value)
    {
        OnPropertyChanged(nameof(CanGoPrevStep));
        OnPropertyChanged(nameof(CanGoNextStep));
        OnPropertyChanged(nameof(WizardStepTitle));
        OnPropertyChanged(nameof(IsListStep));
        OnPropertyChanged(nameof(IsResultStep));
        OnPropertyChanged(nameof(IsIntermediateCreationStep));
        OnPropertyChanged(nameof(IsSavedCalculationsStep));
        OnPropertyChanged(nameof(IsCreationFlowStep));
    }

    partial void OnCompactTablesChanged(bool value)
    {
        OnPropertyChanged(nameof(TableRowHeight));
    }

    private static readonly int[] WizardOrder = [4, 0, 1, 2, 3];

    public bool CanGoPrevStep => Array.IndexOf(WizardOrder, WizardStepIndex) > 0;
    public bool CanGoNextStep => Array.IndexOf(WizardOrder, WizardStepIndex) < WizardOrder.Length - 1;
    public bool IsListStep => WizardStepIndex == 4;
    public bool IsResultStep => WizardStepIndex == 3;
    public bool IsIntermediateCreationStep => IsCreationFlowStep && !IsResultStep;
    public bool IsSavedCalculationsStep => WizardStepIndex == 4;
    public bool IsCreationFlowStep => !IsSavedCalculationsStep;
    public string WizardStepTitle => WizardStepIndex switch
    {
        4 => "Krok 1/5 - Uložené kalkulace",
        0 => "Krok 2/5 - Zdroj dat a výběr",
        1 => "Krok 3/5 - Parametry tisku",
        2 => "Krok 4/5 - Modelování a metadata",
        _ => "Krok 5/5 - Výpočet a uložení"
    };

    [RelayCommand]
    private void PrevStep()
    {
        var currentPos = Array.IndexOf(WizardOrder, WizardStepIndex);
        if (currentPos > 0)
            WizardStepIndex = WizardOrder[currentPos - 1];
    }

    [RelayCommand]
    private async Task NextStep()
    {
        var currentPos = Array.IndexOf(WizardOrder, WizardStepIndex);
        if (currentPos < 0 || currentPos >= WizardOrder.Length - 1) return;

        if (WizardStepIndex == 4)
        {
            WizardStepIndex = 0;
            return;
        }

        // "Potvrdit a pokračovat" = schválení kroku a průběžné uložení.
        // První potvrzení založí kalkulaci, další potvrzení ji aktualizují.
        if (SelectedCalculation is null)
            await SaveAsync();
        else
            await UpdateSelectedCalculationAsync();

        WizardStepIndex = WizardOrder[currentPos + 1];
    }

    [RelayCommand]
    private async Task SaveCurrentCalculationAsync()
    {
        if (SelectedCalculation is null)
            await SaveAsync();
        else
            await UpdateSelectedCalculationAsync();

        WizardStepIndex = 4;
        StatusMessage = "Kalkulace je uložená a zvýrazněná v seznamu.";
    }

    [RelayCommand]
    private void GoToStep(object? index)
    {
        int parsed;
        if (index is int i)
            parsed = i;
        else if (index is string s && int.TryParse(s, out var si))
            parsed = si;
        else
            return;

        if (parsed < 0 || parsed > 4) return;
        WizardStepIndex = parsed;
    }

    [RelayCommand]
    private async Task StartNewCalculationAsync()
    {
        SelectedCalculation = null;
        CustomerSuppliedMaterial = false;
        QuotePrintDescriptionOverride = "";
        QuotePrintLinePreview = "";
        ResultPrintBreakdown = "";
        ResultEnergyBreakdown = "";
        ResultStartFeeBreakdown = "";
        ResultUnitBreakdown = "";
        SlicingFeePerModel = await GetDecimalSettingAsync("Calculation.DefaultSlicingFeePerModel", 100m);
        PostProcessingHours = 0;
        WasteCoefficientPercent = 0;
        StatusMessage = "Nová kalkulace: vyplňte kroky průvodce.";
        WizardStepIndex = 0;
    }

    [RelayCommand]
    private async Task ComputeAsync()
    {
        var electricity = await GetElectricityPriceAsync();
        var tiersRaw = await GetStringSettingAsync("Calculation.QuantityDiscountTiers", "1:0;5:5;20:12");
        var tiers = QuantityDiscountHelper.ParseTiers(tiersRaw);
        var printer = SelectedPrinterId is { } pid
            ? await _db.Printers.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pid)
            : null;

        var input = new CalculationInput
        {
            MaterialGrams = MaterialGrams,
            PrintHours = GetDurationHours(),
            PiecesPerBuild = PiecesPerBuild,
            RequiredPieces = RequiredPieces,
            FilamentPricePerKg = FilamentPricePerKg,
            PrinterHourlyRate = printer?.HourlyRate ?? 0,
            PrinterKwhPerHour = printer?.KwhPerHour ?? 0,
            ModelDesignHours = IncludeModelDesign ? GetModelDesignDurationHours() : 0,
            ModelDesignHourlyRate = IncludeModelDesign ? ModelingHourlyRate : 0,
            StartFeePerPrint = printer?.StartFeePerPrint ?? 0,
            ElectricityPricePerKwh = electricity,
            MarginPercent = MarginPercent,
            CustomerSuppliedMaterial = CustomerSuppliedMaterial,
            SlicingFeePerModel = SlicingFeePerModel,
            PostProcessingHours = PostProcessingHours,
            PostProcessingHourlyRate = PostProcessingHourlyRate,
            WasteCoefficientPercent = WasteCoefficientPercent,
            QuantityDiscountTiers = tiers
        };

        var q = _engine.Compute(input);
        ResultMaterial = q.MaterialCost;
        ResultPrint = q.PrintCost;
        ResultEnergy = q.EnergyCost;
        ResultModelDesign = q.ModelDesignCost;
        ResultStartFee = q.StartFeeCost;
        ResultSlicingFee = q.SlicingFeeCost;
        ResultPostProcessing = q.PostProcessingCost;
        ResultQuantityDiscount = q.QuantityDiscountAmount;
        ResultQuantityDiscountPercent = q.QuantityDiscountPercent;
        ResultSubtotal = q.Subtotal;
        ResultDiscountedSubtotal = q.DiscountedSubtotal;
        ResultTotal = q.TotalWithMargin;
        ResultPrintRuns = q.PrintRuns;
        ResultUnitPrice = q.UnitPriceForRequestedPiece;
        UpdateResultBreakdownTexts(printer, electricity);
    }

    private void UpdateResultBreakdownTexts(Printer? printer, decimal electricityPerKwh)
    {
        var hours = GetDurationHours();
        var runs = Math.Max(1, ResultPrintRuns);
        var rate = printer?.HourlyRate ?? 0;
        var kwh = printer?.KwhPerHour ?? 0;
        var fee = printer?.StartFeePerPrint ?? 0;
        ResultPrintBreakdown =
            $"= {hours:0.###} h × {rate:0.##} Kč/h (hodinovka z karty tiskárny) × {runs} tisků";
        ResultEnergyBreakdown =
            $"= {hours:0.###} h × {kwh:0.##} kWh/h × {electricityPerKwh:0.##} Kč/kWh × {runs} tisků";
        ResultStartFeeBreakdown = fee > 0
            ? $"= {runs} tisků × {fee:0.##} Kč (poplatek/tisk z tiskárny)"
            : $"= {runs} tisků × 0 Kč (poplatek/tisk)";
        var req = Math.Max(1, RequiredPieces);
        ResultUnitBreakdown =
            $"= {ResultTotal:0.##} Kč vč. marže ÷ {req} ks (požadovaný počet)";
        RefreshQuotePrintLinePreview();
    }

    private void RefreshQuotePrintLinePreview()
    {
        var label = string.IsNullOrWhiteSpace(CalculationTitle) ? "Kalkulace" : CalculationTitle.Trim();
        QuotePrintLinePreview = QuoteFromCalculationHelper.BuildPrintLineDescription(
            string.IsNullOrWhiteSpace(QuotePrintDescriptionOverride) ? null : QuotePrintDescriptionOverride,
            CustomerSuppliedMaterial,
            label);
    }

    partial void OnQuotePrintDescriptionOverrideChanged(string value) => RefreshQuotePrintLinePreview();

    partial void OnCalculationTitleChanged(string value) => RefreshQuotePrintLinePreview();

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (ManualPriceEditingEnabled)
            RecalculateManualResults();
        else
            await ComputeAsync();

        var calc = new Calculation
        {
            CustomerId = SelectedCustomerId,
            FilamentTypeId = SelectedFilamentTypeId,
            PrinterId = SelectedPrinterId,
            PrintModelId = SelectedModelId,
            SourceModelPath = ModelPathDisplay is "—" or null or "" ? null : ModelPathDisplay,
            MaterialGrams = MaterialGrams,
            PrintHours = GetDurationHours(),
            PiecesPerBuild = Math.Max(1, PiecesPerBuild),
            RequiredPieces = Math.Max(1, RequiredPieces),
            PrintRuns = Math.Max(1, ResultPrintRuns),
            CustomerSuppliedMaterial = CustomerSuppliedMaterial,
            IncludeModelDesign = IncludeModelDesign,
            ModelDesignHours = GetModelDesignDurationHours(),
            ModelDesignHourlyRate = ModelingHourlyRate,
            MarginPercent = MarginPercent,
            ElectricityPricePerKwh = await GetElectricityPriceAsync(),
            SlicingFeePerModel = SlicingFeePerModel,
            PostProcessingHours = PostProcessingHours,
            PostProcessingHourlyRate = PostProcessingHourlyRate,
            WasteCoefficientPercent = WasteCoefficientPercent,
            MaterialCost = ResultMaterial,
            PrintCost = ResultPrint,
            EnergyCost = ResultEnergy,
            ModelDesignCost = ResultModelDesign,
            StartFeeCost = ResultStartFee,
            SlicingFeeCost = ResultSlicingFee,
            PostProcessingCost = ResultPostProcessing,
            QuantityDiscountPercent = ResultQuantityDiscountPercent,
            QuantityDiscountAmount = ResultQuantityDiscount,
            Subtotal = ResultSubtotal,
            DiscountedSubtotal = ResultDiscountedSubtotal,
            TotalWithMargin = ResultTotal,
            UnitPrice = ResultUnitPrice,
            Title = string.IsNullOrWhiteSpace(CalculationTitle) ? "Kalkulace" : CalculationTitle.Trim(),
            QuotePrintDescriptionOverride = string.IsNullOrWhiteSpace(QuotePrintDescriptionOverride)
                ? null
                : QuotePrintDescriptionOverride.Trim(),
            CreatedAt = DateTime.SpecifyKind(BuildCalculationDateTimeLocal(), DateTimeKind.Local).ToUniversalTime()
        };

        await SaveModelingHourlyRateAsync();
        _db.Calculations.Add(calc);
        await _db.SaveChangesAsync();
        await LoadCalculationsAsync();
        SelectedCalculation = Calculations.FirstOrDefault(c => c.Id == calc.Id);
        StatusMessage = "Kalkulace byla uložena.";
    }

    [RelayCommand]
    private async Task LoadSelectedCalculationAsync()
    {
        var selected = SelectedCalculation;
        if (selected is null)
        {
            StatusMessage = "Vyberte kalkulaci ze seznamu.";
            return;
        }

        var calc = await _db.Calculations.AsNoTracking().FirstOrDefaultAsync(c => c.Id == selected.Id);
        if (calc is null)
        {
            StatusMessage = "Vybraná kalkulace už v databázi neexistuje.";
            await LoadCalculationsAsync();
            return;
        }

        _isLoadingCalculationIntoForm = true;
        try
        {
            SelectedCustomerId = calc.CustomerId;
            SelectedFilamentTypeId = calc.FilamentTypeId;
            SelectedPrinterId = calc.PrinterId;
            SelectedModelId = calc.PrintModelId;
            ModelPathDisplay = string.IsNullOrWhiteSpace(calc.SourceModelPath) ? "—" : calc.SourceModelPath;
            CalculationTitle = calc.Title;
            CalculationDateTime = DateTime.SpecifyKind(calc.CreatedAt, DateTimeKind.Utc).ToLocalTime();
            CalculationTimeHours = CalculationDateTime.Hour;
            CalculationTimeMinutes = CalculationDateTime.Minute;

            MaterialGrams = calc.MaterialGrams;
            SetDurationFromHours(calc.PrintHours);
            PiecesPerBuild = calc.PiecesPerBuild <= 0 ? 1 : calc.PiecesPerBuild;
            RequiredPieces = calc.RequiredPieces <= 0 ? 1 : calc.RequiredPieces;
            CustomerSuppliedMaterial = calc.CustomerSuppliedMaterial;
            QuotePrintDescriptionOverride = calc.QuotePrintDescriptionOverride ?? "";
            IncludeModelDesign = calc.IncludeModelDesign;
            SetModelDesignDurationFromHours(calc.ModelDesignHours);
            ModelingHourlyRate = calc.ModelDesignHourlyRate;
            MarginPercent = calc.MarginPercent;
            SlicingFeePerModel = calc.SlicingFeePerModel;
            PostProcessingHours = calc.PostProcessingHours;
            PostProcessingHourlyRate = calc.PostProcessingHourlyRate;
            WasteCoefficientPercent = calc.WasteCoefficientPercent;

            ResultMaterial = calc.MaterialCost;
            ResultPrint = calc.PrintCost;
            ResultEnergy = calc.EnergyCost;
            ResultModelDesign = calc.ModelDesignCost;
            ResultStartFee = calc.StartFeeCost;
            ResultSlicingFee = calc.SlicingFeeCost;
            ResultPostProcessing = calc.PostProcessingCost;
            ResultQuantityDiscount = calc.QuantityDiscountAmount;
            ResultQuantityDiscountPercent = calc.QuantityDiscountPercent;
            ResultSubtotal = calc.Subtotal;
            ResultDiscountedSubtotal = calc.DiscountedSubtotal;
            ResultTotal = calc.TotalWithMargin;
            ResultPrintRuns = calc.PrintRuns <= 0 ? 1 : calc.PrintRuns;
            ResultUnitPrice = calc.UnitPrice <= 0 && RequiredPieces > 0
                ? Math.Round(calc.TotalWithMargin / RequiredPieces, 2, MidpointRounding.AwayFromZero)
                : calc.UnitPrice;
        }
        finally
        {
            _isLoadingCalculationIntoForm = false;
        }

        var printerForHint = SelectedPrinterId is { } pid
            ? await _db.Printers.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pid)
            : null;
        var electricity = await GetElectricityPriceAsync();
        UpdateResultBreakdownTexts(printerForHint, electricity);

        StatusMessage = $"Kalkulace {calc.Id} načtena do formuláře.";
    }

    [RelayCommand]
    private async Task OpenSelectedForEditAsync()
    {
        if (SelectedCalculation is null)
        {
            StatusMessage = "Vyberte kalkulaci ze seznamu.";
            return;
        }

        await LoadSelectedCalculationAsync();
        WizardStepIndex = 0;
    }

    [RelayCommand]
    private async Task UpdateSelectedCalculationAsync()
    {
        var selected = SelectedCalculation;
        if (selected is null)
        {
            StatusMessage = "Vyberte kalkulaci ze seznamu.";
            return;
        }

        if (ManualPriceEditingEnabled)
            RecalculateManualResults();
        else
            await ComputeAsync();
        var tracked = await _db.Calculations.FirstOrDefaultAsync(c => c.Id == selected.Id);
        if (tracked is null)
        {
            StatusMessage = "Vybraná kalkulace už v databázi neexistuje.";
            await LoadCalculationsAsync();
            return;
        }

        tracked.CustomerId = SelectedCustomerId;
        tracked.FilamentTypeId = SelectedFilamentTypeId;
        tracked.PrinterId = SelectedPrinterId;
        tracked.PrintModelId = SelectedModelId;
        tracked.SourceModelPath = ModelPathDisplay is "—" or null or "" ? null : ModelPathDisplay;
        tracked.MaterialGrams = MaterialGrams;
        tracked.PrintHours = GetDurationHours();
        tracked.PiecesPerBuild = Math.Max(1, PiecesPerBuild);
        tracked.RequiredPieces = Math.Max(1, RequiredPieces);
        tracked.PrintRuns = Math.Max(1, ResultPrintRuns);
        tracked.CustomerSuppliedMaterial = CustomerSuppliedMaterial;
        tracked.QuotePrintDescriptionOverride = string.IsNullOrWhiteSpace(QuotePrintDescriptionOverride)
            ? null
            : QuotePrintDescriptionOverride.Trim();
        tracked.IncludeModelDesign = IncludeModelDesign;
        tracked.ModelDesignHours = GetModelDesignDurationHours();
        tracked.ModelDesignHourlyRate = ModelingHourlyRate;
        tracked.MarginPercent = MarginPercent;
        tracked.ElectricityPricePerKwh = await GetElectricityPriceAsync();
        tracked.SlicingFeePerModel = SlicingFeePerModel;
        tracked.PostProcessingHours = PostProcessingHours;
        tracked.PostProcessingHourlyRate = PostProcessingHourlyRate;
        tracked.WasteCoefficientPercent = WasteCoefficientPercent;
        tracked.MaterialCost = ResultMaterial;
        tracked.PrintCost = ResultPrint;
        tracked.EnergyCost = ResultEnergy;
        tracked.ModelDesignCost = ResultModelDesign;
        tracked.StartFeeCost = ResultStartFee;
        tracked.SlicingFeeCost = ResultSlicingFee;
        tracked.PostProcessingCost = ResultPostProcessing;
        tracked.QuantityDiscountPercent = ResultQuantityDiscountPercent;
        tracked.QuantityDiscountAmount = ResultQuantityDiscount;
        tracked.Subtotal = ResultSubtotal;
        tracked.DiscountedSubtotal = ResultDiscountedSubtotal;
        tracked.TotalWithMargin = ResultTotal;
        tracked.UnitPrice = ResultUnitPrice;
        tracked.Title = string.IsNullOrWhiteSpace(CalculationTitle) ? "Kalkulace" : CalculationTitle.Trim();
        tracked.CreatedAt = DateTime.SpecifyKind(BuildCalculationDateTimeLocal(), DateTimeKind.Local).ToUniversalTime();

        await SaveModelingHourlyRateAsync();
        await _db.SaveChangesAsync();
        await LoadCalculationsAsync();
        SelectedCalculation = Calculations.FirstOrDefault(c => c.Id == tracked.Id);
        StatusMessage = $"Kalkulace {tracked.Id} byla upravena.";
    }

    [RelayCommand]
    private async Task CreateQuoteFromCalculationAsync()
    {
        if (SelectedCalculation is null)
        {
            StatusMessage = "Vyberte kalkulaci ze seznamu.";
            return;
        }

        var calc = await _db.Calculations.AsNoTracking().FirstOrDefaultAsync(c => c.Id == SelectedCalculation.Id);
        if (calc is null)
        {
            StatusMessage = "Vybraná kalkulace už v databázi neexistuje.";
            await LoadCalculationsAsync();
            return;
        }

        if (calc.CustomerId is null)
        {
            StatusMessage = "Vybraná kalkulace nemá přiřazeného zákazníka.";
            return;
        }

        var num = await _numbers.NextQuoteNumberAsync();
        var q = new Quote
        {
            CustomerId = calc.CustomerId.Value,
            Number = num,
            Title = calc.Title,
            IssueDate = DateTime.UtcNow,
            Status = QuoteStatus.Draft,
            SourceCalculationId = calc.Id,
            TotalAmount = calc.TotalWithMargin
        };
        QuoteFromCalculationHelper.AddDetailedLines(q, calc);
        q.TotalAmount = q.Lines.Sum(x => x.LineTotal);

        _db.Quotes.Add(q);
        await _db.SaveChangesAsync();
        StatusMessage = $"Vytvořena nabídka {q.Number}.";
    }

    [RelayCommand]
    private async Task RefreshCalculationsAsync() => await LoadCalculationsAsync();

    [RelayCommand]
    private async Task DeleteSelectedCalculationAsync()
    {
        if (SelectedCalculation is null)
        {
            StatusMessage = "Vyberte kalkulaci ze seznamu.";
            return;
        }

        var selectedId = SelectedCalculation.Id;
        var linkedQuotes = await _db.Quotes
            .Where(q => q.SourceCalculationId == selectedId)
            .ToListAsync();

        var deleteLinkedQuotes = false;
        if (linkedQuotes.Count > 0)
        {
            var answer = AppDialog.ShowQuestion(
                $"Kalkulace má {linkedQuotes.Count} navázaných nabídek.\n\n" +
                "Ano = smazat pouze kalkulaci (nabídky zůstanou a vazba se zruší).\n" +
                "Ne = smazat kalkulaci i navázané nabídky.\n" +
                "Storno = nic neměnit.",
                "Smazat kalkulaci",
                MessageBoxButton.YesNoCancel);

            if (answer == MessageBoxResult.Cancel)
                return;

            deleteLinkedQuotes = answer == MessageBoxResult.No;
        }
        else
        {
            var confirm = AppDialog.ShowQuestion(
                "Opravdu chcete smazat vybranou kalkulaci?",
                "Smazat kalkulaci",
                MessageBoxButton.YesNo);
            if (confirm != MessageBoxResult.Yes)
                return;
        }

        var calc = await _db.Calculations.FirstOrDefaultAsync(c => c.Id == selectedId);
        if (calc is null)
        {
            StatusMessage = "Kalkulace už neexistuje.";
            await LoadCalculationsAsync();
            return;
        }

        if (deleteLinkedQuotes)
            _db.Quotes.RemoveRange(linkedQuotes);

        _db.Calculations.Remove(calc);
        await _db.SaveChangesAsync();
        await LoadCalculationsAsync();
        SelectedCalculation = Calculations.FirstOrDefault();

        StatusMessage = deleteLinkedQuotes
            ? "Kalkulace i navázané nabídky byly smazány."
            : "Kalkulace byla smazána. Navázané nabídky zůstaly bez vazby.";
    }

    [RelayCommand]
    private async Task ExportSelectedCalculationPdfAsync()
    {
        await ComputeAsync();
        var previewSource = SelectedCalculation ?? await BuildDraftCalculationForPreviewAsync();
        var preview = new CalculationPreviewWindow(previewSource)
        {
            Owner = Application.Current?.MainWindow
        };
        if (preview.ShowDialog() != true) return;
        var dir = await GetExportPathAsync("Export.CalculationsPdfPath", "Kalkulace");
        var path = await _calculationPdf.SaveCalculationPdfAsync(previewSource, dir);
        AppDialog.ShowInfo($"PDF uloženo:\n{path}", "Náhled kalkulace");
    }

    private async Task<decimal> GetDecimalSettingAsync(string key, decimal fallback)
    {
        var row = await _db.AppSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Key == key);
        return row is not null && decimal.TryParse(row.Value.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d)
            ? d
            : fallback;
    }

    private async Task<string> GetStringSettingAsync(string key, string fallback)
    {
        var row = await _db.AppSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Key == key);
        return string.IsNullOrWhiteSpace(row?.Value) ? fallback : row!.Value;
    }

    private async Task<decimal> GetElectricityPriceAsync()
    {
        var row = await _db.AppSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Key == "ElectricityPricePerKwh");
        return row is not null && decimal.TryParse(row.Value.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d)
            ? d
            : 7.5m;
    }

    private async Task<decimal> GetModelingHourlyRateAsync()
    {
        var row = await _db.AppSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Key == "ModelingHourlyRate");
        return row is not null && decimal.TryParse(row.Value.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d)
            ? d
            : 450m;
    }

    private async Task<string> GetDataRootPathAsync()
    {
        var row = await _db.AppSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Key == "App.DataRootPath");
        if (row is null || string.IsNullOrWhiteSpace(row.Value))
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PrintCalc");
        return row.Value;
    }

    private async Task<string> GetExportPathAsync(string key, string fallbackFolder)
    {
        var row = await _db.AppSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Key == key);
        if (row is not null && !string.IsNullOrWhiteSpace(row.Value))
            return row.Value;
        return Path.Combine(await GetDataRootPathAsync(), fallbackFolder);
    }

    private async Task SaveModelingHourlyRateAsync()
    {
        var row = await _db.AppSettings.FirstOrDefaultAsync(x => x.Key == "ModelingHourlyRate");
        if (row is null)
        {
            row = new AppSettingsRow { Key = "ModelingHourlyRate" };
            _db.AppSettings.Add(row);
        }
        row.Value = ModelingHourlyRate.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    [RelayCommand]
    private async Task IssueMaterialAsync()
    {
        if (CustomerSuppliedMaterial)
        {
            StatusMessage = "U materiálu zákazníka se skladový výdej neprovádí.";
            return;
        }

        if (SelectedFilamentTypeId is not { } id) return;
        var wasteMul = 1m + Math.Max(0, WasteCoefficientPercent) / 100m;
        var printRuns = Math.Max(1, ResultPrintRuns);
        var kg = MaterialGrams / 1000m * printRuns * wasteMul;
        if (kg <= 0) return;
        var calcId = SelectedCalculation?.Id;
        await _stock.IssueAsync(id, kg, calcId is { } cid ? $"Výdej z kalkulace #{cid}" : "Výdej z kalkulace", calcId);
        await LoadAsync();
        ApplyFilamentTypeId(id);
        StatusMessage = "Materiál byl vydán ze skladu.";
    }

    private decimal GetDurationHours()
    {
        var h = Math.Max(0, PrintDurationHours);
        var m = Math.Max(0, PrintDurationMinutes);
        return h + m / 60m;
    }

    private decimal GetModelDesignDurationHours()
    {
        var h = Math.Max(0, ModelDesignDurationHours);
        var m = Math.Max(0, ModelDesignDurationMinutes);
        return h + m / 60m;
    }

    private void SetDurationFromHours(decimal totalHours)
    {
        if (totalHours < 0) totalHours = 0;
        var hours = (int)Math.Floor(totalHours);
        var minutes = (int)Math.Round((totalHours - hours) * 60m, MidpointRounding.AwayFromZero);
        if (minutes >= 60)
        {
            hours += 1;
            minutes -= 60;
        }
        PrintDurationHours = hours;
        PrintDurationMinutes = minutes;
    }

    private void SetModelDesignDurationFromHours(decimal totalHours)
    {
        if (totalHours < 0) totalHours = 0;
        var hours = (int)Math.Floor(totalHours);
        var minutes = (int)Math.Round((totalHours - hours) * 60m, MidpointRounding.AwayFromZero);
        if (minutes >= 60)
        {
            hours += 1;
            minutes -= 60;
        }
        ModelDesignDurationHours = hours;
        ModelDesignDurationMinutes = minutes;
    }

    private DateTime BuildCalculationDateTimeLocal()
    {
        var date = CalculationDateTime.Date;
        var h = Math.Clamp(CalculationTimeHours, 0, 23);
        var m = Math.Clamp(CalculationTimeMinutes, 0, 59);
        return date.AddHours(h).AddMinutes(m);
    }

    private async Task<Calculation> BuildDraftCalculationForPreviewAsync()
    {
        return new Calculation
        {
            Id = 0,
            CustomerId = SelectedCustomerId,
            FilamentTypeId = SelectedFilamentTypeId,
            PrinterId = SelectedPrinterId,
            PrintModelId = SelectedModelId,
            SourceModelPath = ModelPathDisplay is "—" or null or "" ? null : ModelPathDisplay,
            MaterialGrams = MaterialGrams,
            PrintHours = GetDurationHours(),
            PiecesPerBuild = Math.Max(1, PiecesPerBuild),
            RequiredPieces = Math.Max(1, RequiredPieces),
            PrintRuns = Math.Max(1, ResultPrintRuns),
            CustomerSuppliedMaterial = CustomerSuppliedMaterial,
            IncludeModelDesign = IncludeModelDesign,
            ModelDesignHours = GetModelDesignDurationHours(),
            ModelDesignHourlyRate = ModelingHourlyRate,
            MarginPercent = MarginPercent,
            ElectricityPricePerKwh = await GetElectricityPriceAsync(),
            SlicingFeePerModel = SlicingFeePerModel,
            PostProcessingHours = PostProcessingHours,
            PostProcessingHourlyRate = PostProcessingHourlyRate,
            WasteCoefficientPercent = WasteCoefficientPercent,
            MaterialCost = ResultMaterial,
            PrintCost = ResultPrint,
            EnergyCost = ResultEnergy,
            ModelDesignCost = ResultModelDesign,
            StartFeeCost = ResultStartFee,
            SlicingFeeCost = ResultSlicingFee,
            PostProcessingCost = ResultPostProcessing,
            QuantityDiscountPercent = ResultQuantityDiscountPercent,
            QuantityDiscountAmount = ResultQuantityDiscount,
            Subtotal = ResultSubtotal,
            DiscountedSubtotal = ResultDiscountedSubtotal,
            TotalWithMargin = ResultTotal,
            UnitPrice = ResultUnitPrice,
            Title = string.IsNullOrWhiteSpace(CalculationTitle) ? "Kalkulace" : CalculationTitle.Trim(),
            QuotePrintDescriptionOverride = string.IsNullOrWhiteSpace(QuotePrintDescriptionOverride)
                ? null
                : QuotePrintDescriptionOverride.Trim(),
            CreatedAt = DateTime.SpecifyKind(BuildCalculationDateTimeLocal(), DateTimeKind.Local).ToUniversalTime()
        };
    }

    partial void OnResultMaterialChanged(decimal value)
    {
        if (ManualPriceEditingEnabled) RecalculateManualResults();
    }

    partial void OnResultPrintChanged(decimal value)
    {
        if (ManualPriceEditingEnabled) RecalculateManualResults();
    }

    partial void OnResultEnergyChanged(decimal value)
    {
        if (ManualPriceEditingEnabled) RecalculateManualResults();
    }

    partial void OnResultModelDesignChanged(decimal value)
    {
        if (ManualPriceEditingEnabled) RecalculateManualResults();
    }

    partial void OnResultStartFeeChanged(decimal value)
    {
        if (ManualPriceEditingEnabled) RecalculateManualResults();
    }

    partial void OnResultSlicingFeeChanged(decimal value)
    {
        if (ManualPriceEditingEnabled) RecalculateManualResults();
    }

    partial void OnResultPostProcessingChanged(decimal value)
    {
        if (ManualPriceEditingEnabled) RecalculateManualResults();
    }

    partial void OnMarginPercentChanged(decimal value)
    {
        if (ManualPriceEditingEnabled) RecalculateManualResults();
    }

    partial void OnRequiredPiecesChanged(int value)
    {
        if (ManualPriceEditingEnabled) RecalculateManualResults();
        OnPropertyChanged(nameof(MaterialGramsPerPieceDisplay));
        OnPropertyChanged(nameof(PrintHoursPerPieceDisplay));
    }

    partial void OnManualPriceEditingEnabledChanged(bool value)
    {
        if (value) RecalculateManualResults();
    }

    private void RecalculateManualResults()
    {
        ResultMaterial = RoundMoney(ResultMaterial);
        ResultPrint = RoundMoney(ResultPrint);
        ResultEnergy = RoundMoney(ResultEnergy);
        ResultModelDesign = RoundMoney(ResultModelDesign);
        ResultStartFee = RoundMoney(ResultStartFee);
        ResultSlicingFee = RoundMoney(ResultSlicingFee);
        ResultPostProcessing = RoundMoney(ResultPostProcessing);

        ResultSubtotal = RoundMoney(ResultMaterial + ResultPrint + ResultEnergy + ResultModelDesign + ResultStartFee + ResultSlicingFee + ResultPostProcessing);
        ResultQuantityDiscount = RoundMoney(ResultSubtotal * (ResultQuantityDiscountPercent / 100m));
        ResultDiscountedSubtotal = RoundMoney(ResultSubtotal - ResultQuantityDiscount);
        ResultTotal = RoundMoney(ResultDiscountedSubtotal * (1m + MarginPercent / 100m));
        var pieces = Math.Max(1, RequiredPieces);
        ResultUnitPrice = RoundMoney(ResultTotal / pieces);
        ResultUnitBreakdown = $"= {ResultTotal:0} Kč vč. marže ÷ {pieces} ks (požadovaný počet)";
    }

    private static decimal RoundMoney(decimal value) =>
        Math.Round(value, 0, MidpointRounding.AwayFromZero);

    partial void OnCustomerSuppliedMaterialChanged(bool value)
    {
        if (_isLoadingCalculationIntoForm) return;
        _ = ComputeAsync();
    }

    [RelayCommand]
    private void SetModelingOnlyMode()
    {
        IncludeModelDesign = true;
        CustomerSuppliedMaterial = false;
        MaterialGrams = 0;
        PrintDurationHours = 0;
        PrintDurationMinutes = 0;
        SelectedPrinterId = null;
        SelectedFilamentTypeId = null;
        StatusMessage = "Režim pouze modelování je zapnutý.";
    }
}
