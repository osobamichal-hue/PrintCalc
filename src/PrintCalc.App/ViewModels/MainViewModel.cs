using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using PrintCalc.App.Views;

namespace PrintCalc.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IServiceProvider _sp;

    public MainViewModel(IServiceProvider sp)
    {
        _sp = sp;
        Menu =
        [
            new NavEntry("Zákazníci", "\uE77B", typeof(CustomersView)),
            new NavEntry("Filamenty a sklad", "\uE8FD", typeof(FilamentsView)),
            new NavEntry("Tiskárny", "\uE7F8", typeof(PrintersView)),
            new NavEntry("Modely (STL/3MF)", "\uE1D3", typeof(ModelsView)),
            new NavEntry("Kalkulace", "\uE9D2", typeof(CalculationView)),
            new NavEntry("Nabídky", "\uE70B", typeof(QuotesView)),
            new NavEntry("Zakázky", "\uE7C1", typeof(OrdersView)),
            new NavEntry("Faktury", "\uE8A1", typeof(InvoicesView)),
            new NavEntry("Nastavení", "\uE713", typeof(CompanySettingsView)),
            new NavEntry("Nápověda a o aplikaci", "\uE897", typeof(HelpAboutView))
        ];
    }

    public ObservableCollection<NavEntry> Menu { get; }

    [ObservableProperty] private NavEntry? selectedMenu;

    [ObservableProperty] private object? currentPage;

    partial void OnSelectedMenuChanged(NavEntry? value)
    {
        if (value is null) return;
        CurrentPage = _sp.GetRequiredService(value.ViewType);
    }

    public void NavigateHome() => SelectedMenu = Menu[0];
}

public record NavEntry(string Title, string Glyph, Type ViewType);
