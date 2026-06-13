using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PrintCalc.Infrastructure.Rendering;

/// <summary>Stejné pruhování jako v UI nabídky — podle <see cref="PrintCalc.Core.Entities.QuoteLine.SourceCalculationId"/> / řádků dokladů.</summary>
public static class CustomerDocumentLineStripe
{
    public static Color? RowBackgroundForGroup<T>(IReadOnlyList<T> orderedById, int rowIndex, Func<T, int?> getSourceCalculationId)
    {
        if (rowIndex < 0 || rowIndex >= orderedById.Count) return null;
        if (getSourceCalculationId(orderedById[rowIndex]) is null) return null;
        var band = 0;
        for (var i = 1; i <= rowIndex; i++)
        {
            var prev = getSourceCalculationId(orderedById[i - 1]);
            var cur = getSourceCalculationId(orderedById[i]);
            if (prev != cur) band++;
        }
        return band % 2 == 0 ? Colors.Purple.Lighten5 : Colors.Blue.Lighten5;
    }

    public static string CalcColumnText(int? sourceCalculationId) =>
        sourceCalculationId is { } id ? $"#{id}" : "—";
}
