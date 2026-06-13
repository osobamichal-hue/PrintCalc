# PrintCalc - 3D tisk kalkulace a evidence (WPF/.NET 8)

## Spuštění

Z **kořene** repozitáře (složka, kde je `src\`):

```powershell
dotnet run --project src\PrintCalc.App\PrintCalc.App.csproj
```

**Verze aplikace:** 1.1.0 (viz `Version` v `PrintCalc.App.csproj`)

Desktop aplikace pro firemni evidenci 3D tisku:

- zakaznici
- filamenty + sklad
- tiskarny
- kalkulace ceny
- nabidky, zakazky, faktury
- export faktury do PDF/CSV

## Technologie

- C# / .NET 8
- WPF (MVVM)
- Entity Framework Core
- SQLite (vychozi) + priprava na PostgreSQL

## Pozadavky

- .NET 8 SDK
- Windows 10/11

Oveeni:

```powershell
dotnet --version
dotnet ef --version
```

## Spuštění – poznámky

Příkaz ke spuštění je na **začátku** tohoto souboru (sekce **Spuštění**).

Po prvnim startu se automaticky vytvori SQLite DB v:

`%LocalAppData%\PrintCalc\printcalc.db`

## Databaze

Konfigurace je v:

`src/PrintCalc.App/appsettings.json`

Vychozi:

- provider: `Sqlite`
- PostgreSQL je dostupne po prepnuti:
  - `Database:Provider = "Npgsql"`
  - `ConnectionStrings:PostgreSQL = "..."`

Migrace se aplikuji pri startu aplikace (`Database.Migrate()`).

## Moduly a workflow

### 1) Zakaznici

- CRUD evidencnich udaju
- vazba na kalkulace / nabidky / zakazky / faktury

### 2) Filamenty a sklad (tři karty / průvodce)

Modul je rozdělen do **tří karet** ve stylu průvodce (jako kalkulace): horní odkazovací tlačítka, velké záložky, dole **Zpět** / **Dále**.

**Krok 1 – Správa filamentů**

- pouze evidence **typu** (`FilamentType`): přidání, úprava v tabulce, uložení, smazání
- import **QR kódu** z JPG/PNG (doplnění údajů typu / příprava pro příjem)
- **zde nejsou** tlačítka příjem/výdej skladu (patří na kartu Sklad)

**Krok 2 – Sklad**

- přehled **skladových karet** (`FilamentStock`: šarže, expirace, dodavatel, kg, kusy, …)
- filtry: všechny typy / jen vybraný typ, jen aktivní karty (zbývá > 0)
- **Příjem na sklad** a **Výdej ze skladu** (dialogy s průvodcem krok za krokem)
- kontextové menu na mřížce skladu

**Krok 3 – Pohyby**

- přehled **StockMovement** (posledních 500 záznamů): datum/čas, typ (příjem / výdej / inventura), filament, změna kg, cena/kg, poznámka
- po příjmu/výdeji se seznam obnoví (tlačítko **Obnovit** načte znovu i typy a sklad)

Datovy model:

- `FilamentType` (typ/material)
- `FilamentStock` (sarze / karta zasoby)
- `StockMovement` (audit pohybu)
- vážený průměr ceny (Kč/kg) z aktuálních zásob

Další operace (API/služby):

- inventurní úprava je dostupná ve skladové službě (`AdjustInventoryAsync`), UI ji lze doplnit později

Poznámka k mazání typu:

- mazání `FilamentType` je ošetřeno tak, aby nejdřív odstranilo/navázalo závislé záznamy (stock, movement, links, kalkulace FK), aby nevznikal `FOREIGN KEY constraint failed`.

### 3) Tiskarny

Pole tiskarny:

- hodinova sazba
- kWh/h
- pevny poplatek za tisk (`StartFeePerPrint`)

`StartFeePerPrint` je urcen pro pripravu tisku (nahreti, cisteni, adheze).

### 4) Kalkulace

- průvodce po **krocích**; výchozí karta je **uložené kalkulace**, další kroky: zdroj dat, parametry tisku, modelování, výpočet
- **potvrzení kroku** (`Potvrdit a pokračovat`) průběžně ukládá nebo aktualizuje rozpracovanou kalkulaci
- podpora **kusů na podložce** a **požadovaného počtu kusů** → počet tisků, cena za 1 ks, celkem

Vstupy:

- zakaznik
- filament
- tiskarna
- modelovy soubor (3MF nebo GCode)
- hmotnost (g) - desetinne cislo
- cas tisku (h + min)
- marze (%)

Vzorec:

- material = `(g / 1000) * cena_filamentu`
- tisk = `hodiny * hodinova_sazba`
- energie = `hodiny * kWh/h * cena_kWh`
- start fee = `pevny_poplatek_za_tisk`
- mezisoucet = soucet vsech polozek
- vysledek = mezisoucet + marze

Dalsi akce:

- ulozit novou kalkulaci
- nacist ulozenou kalkulaci do formulare
- upravit vybranou kalkulaci
- vydat material ze skladu
- vytvorit nabidku z vybrane kalkulace

### 5) Nabídky → Zakázky → Faktury

- Nabídka může vzniknout přímo z kalkulace.
- Zakázka může vzniknout z nabídky.
- Faktura může vzniknout ze zakázky.

**Režim řádků při tvorbě z více zdrojů**

- **Nabídky** z kalkulací: **detailní** (modelování + tisk na kusy + případná korekce) nebo **souhrnná** (jeden řádek na kalkulaci). Volba se ukládá; stejný význam má přepínač v **Nastavení → Aplikace → Výchozí režim převodu dokladů** (`Quotes.CreateAsDetailedCalculation`).
- **Zakázky** z více nabídek: **detailní převod položek** vs **souhrn** (řádek = celá nabídka). Nastavení: `Orders.CreateAsDetailedFromQuotes` (+ přepínač v modulu a v Nastavení).
- **Faktury** z více zakázek: obdobně (`Invoices.CreateAsDetailedFromOrders`).

Export:

- PDF faktury
- CSV (zaklad pro ABRA/Flexi import)

## Import souboru (3MF/GCode)

V kalkulaci lze soubor nacist:

- drag&drop do vyznacene zony
- tlacitkem "Vybrat 3MF/GCode soubor..."

Podporovane pripony:

- `.3mf`
- `.gcode`
- `.gco`

### 3MF parser

Snazi se nacist:

- materialovou spotrebu (g)
- cas tisku (h)
- dalsi metadata z XML/atributu

Podporuje mimo jine formaty:

- `2:33:00`
- `2h33m`
- `14m43s`
- `98,34 g` / `0,098 kg`

### GCode parser

Snazi se nacist:

- `;TIME:...` (sekundy)
- `;TIME_ELAPSED:...`
- `M73 P.. R..` (remaining time)
- komentare s vahou v `g`/`kg`

Pokud nektera data v souboru chybi, aplikace upozorni a je potreba je dopsat rucne.

## UX a zadavani cisel

- decimal pole berou carku i tecku (`0,08` i `0.08`)
- u casu se zadava zvlast `h` a `min`
- stavove hlasky v kalkulaci potvrzuji nacteni/ulozeni/prevody

## Reseni problemu

### "Spustim z CMD a nic se nevypise"

To je normalni chovani WPF (`WinExe`) - okno se otevre, konzole nemusi logovat.

Doporucene spusteni:

```powershell
dotnet run --project src\PrintCalc.App\PrintCalc.App.csproj
```

### Build selze s lockem DLL/editorconfig

Aplikace je pravdepodobne stale spustena.

- zavrit `PrintCalc.App.exe`
- spustit build znovu

### 3MF/GCode nevyplni vsechna data

Soubor casto neobsahuje vsechny metadata.

- doplnit rucne
- pouzit export "project 3MF" ze sliceru
- u GCode preferovat variantu s `M73`, `TIME` a komentarovymi statistikami

## Nastavení (centrální)

Modul **Nastavení** (dříve „Moje firma“) obsahuje záložky **Firma**, **Aplikace**, **Šablony a výstupy**, **Finance**.

V **Aplikaci** lze mimo jiné nastavit:

- motiv (světlý/tmavý), paletu barev, datovou složku
- **výchozí režim převodu dokladů** (stejné klíče jako u modulů Nabídky / Zakázky / Faktury – viz výše)

## Build release (bez instalatoru)

```powershell
dotnet build src\PrintCalc.App\PrintCalc.App.csproj -c Release
```

Vystup: `src\PrintCalc.App\bin\Release\net8.0-windows\`

Self-contained publish (jeden adresar k rozdistribuovani bez instalovaneho .NET runtime na cilovem PC):

```powershell
dotnet publish src\PrintCalc.App\PrintCalc.App.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
```

## Distribuce a instalátor

**Instalátor se nevyrobí příkazem `dotnet build` ani samotným `dotnet publish` bez dalších kroků.** Stejně jako dříve musíte spustit skript níže – výstup je vždy:

`artifacts\installer\3DPrintCalc-Setup.exe`

V repozitáři je připraven pipeline:

- `installer/build-installer.ps1` – vyčistí `artifacts`, provede self-contained single-file **publish** do `artifacts\publish\win-x64`, pak zavolá Inno Setup
- `installer/PrintCalcSetup.iss` – definice instalátoru

Spuštění (číslo verze sjednoťte s `Version` v `PrintCalc.App.csproj`):

```powershell
powershell -ExecutionPolicy Bypass -File installer\build-installer.ps1 -Version 1.1.0
```

Výstupy:

- publish: `artifacts\publish\win-x64\`
- instalátor (vyžaduje **Inno Setup 6**, případně `iscc.exe` v PATH nebo v `%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe`): **`artifacts\installer\3DPrintCalc-Setup.exe`**

Poznámka:

- Pokud Inno Setup není nainstalovaný, skript dokončí jen publish a vypíše návod, jak `Setup.exe` dodělat ručně.

## Struktura projektu

- `src/PrintCalc.Core` - domena, entity, rozhrani sluzeb
- `src/PrintCalc.Infrastructure` - EF Core, parsery, sklad, exporty
- `src/PrintCalc.App` - WPF UI, ViewModels, navigace

## Poznamky

- Dokumentace odrazi aktualni stav implementace.
- Pokud budete chtit, lze doplnit i technickou dokumentaci API/sluzeb po souborech (Core/Infrastructure/App) a uzivatelsky manual krok-za-krokem.
