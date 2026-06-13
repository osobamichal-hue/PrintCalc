using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows;
using PrintCalc.App.Views;

namespace PrintCalc.App.ViewModels;

public partial class HelpAboutViewModel : ObservableObject
{
    private const string EmbeddedDocumentation = """
PrintCalc - stručná nápověda
============================

1) Nastavení
- V modulu Nastavení vyplňte firemní údaje, finance, cesty exportů a datovou složku.
- Doporučeno: zapnout pravidelné zálohování.

2) Filamenty a sklad
- Vytvořte skladové karty filamentů (typ, výrobce, průměr, hustota, teploty).
- Proveďte příjem na sklad, aby výpočty měly správné ceny materiálu.

3) Kalkulace
- Vyberte zákazníka, filament, tiskárnu a parametry tisku.
- Uložte kalkulaci, případně exportujte PDF.

4) Tok dokladů
- Z kalkulace vytvořte nabídku.
- Z nabídky vytvořte zakázku.
- Ze zakázky vytvořte fakturu.

5) Zálohování a obnova
- Záloha ukládá databázi i data aplikace.
- Obnova vrací stav ze zálohy, po obnově se doporučuje restart aplikace.

Poznámka:
- Při upgrade aplikace se databáze migruje automaticky při startu.
""";

    public string AppName => "PrintCalc - 3D tisk";
    public string Version => Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "neznámá";
    public string Runtime => System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
    public string Os => Environment.OSVersion.VersionString;

    [RelayCommand]
    private void OpenDocumentation()
    {
        var window = new DocumentationWindow
        {
            Owner = Application.Current?.MainWindow,
            DocumentationText = EmbeddedDocumentation
        };
        window.ShowDialog();
    }
}
