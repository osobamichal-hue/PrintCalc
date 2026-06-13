using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace PrintCalc.App.Helpers;

/// <summary>
/// Obnovení vázané kolekce na UI vlákně až po dokončení rozpracované editace DataGridu,
/// aby nedocházelo k InvalidOperationException (DeferRefresh během EditItem).
/// </summary>
public static class DispatcherObservableRefresh
{
    public static async Task ReplaceAsync<T>(ObservableCollection<T> target, IReadOnlyList<T> source)
    {
        void Apply()
        {
            target.Clear();
            foreach (var item in source)
                target.Add(item);
        }

        if (Application.Current?.Dispatcher is not { } dispatcher)
        {
            Apply();
            return;
        }

        await dispatcher.InvokeAsync(Apply, DispatcherPriority.ApplicationIdle);
    }
}
