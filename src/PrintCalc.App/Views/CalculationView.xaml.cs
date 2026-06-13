using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using PrintCalc.App.Dnd;
using PrintCalc.App.ViewModels;
using PrintCalc.Core.Entities;

namespace PrintCalc.App.Views;

public partial class CalculationView : UserControl
{
    private Point _dragStart;

    public CalculationView(CalculationViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    private CalculationViewModel Vm => (CalculationViewModel)DataContext;

    private void List_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) =>
        _dragStart = e.GetPosition(null);

    private void FilamentList_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || sender is not ListBox lb || lb.SelectedItem is not FilamentType ft)
            return;

        var pos = e.GetPosition(null);
        if ((pos - _dragStart).Length < 4)
            return;

        var data = new DataObject(PrintCalcDragFormats.FilamentTypeId, ft.Id);
        System.Windows.DragDrop.DoDragDrop(lb, data, DragDropEffects.Copy);
    }

    private void PrinterList_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || sender is not ListBox lb || lb.SelectedItem is not Printer p)
            return;

        var pos = e.GetPosition(null);
        if ((pos - _dragStart).Length < 4)
            return;

        var data = new DataObject(PrintCalcDragFormats.PrinterId, p.Id);
        System.Windows.DragDrop.DoDragDrop(lb, data, DragDropEffects.Copy);
    }

    private void DropZone_OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = DragDropEffects.None;
        if (e.Data.GetDataPresent(PrintCalcDragFormats.FilamentTypeId) ||
            e.Data.GetDataPresent(PrintCalcDragFormats.PrinterId) ||
            e.Data.GetDataPresent(DataFormats.FileDrop))
            e.Effects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private void DropZone_OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(PrintCalcDragFormats.FilamentTypeId))
        {
            var id = (int)e.Data.GetData(PrintCalcDragFormats.FilamentTypeId);
            Vm.ApplyFilamentTypeId(id);
        }

        if (e.Data.GetDataPresent(PrintCalcDragFormats.PrinterId))
        {
            var id = (int)e.Data.GetData(PrintCalcDragFormats.PrinterId);
            Vm.ApplyPrinterId(id);
        }

        if (TryExtractModelPath(e.Data, out var modelPath))
            Vm.ApplyModelFile(modelPath);

        e.Handled = true;
    }

    private void Pick3Mf_OnClick(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Vyberte model (3MF nebo GCode)",
            Filter = "Modely (*.3mf;*.gcode;*.gco)|*.3mf;*.gcode;*.gco|3MF soubory (*.3mf)|*.3mf|GCode soubory (*.gcode;*.gco)|*.gcode;*.gco|Všechny soubory (*.*)|*.*"
        };

        if (dlg.ShowDialog() == true)
            Vm.ApplyModelFile(dlg.FileName);
    }

    private void CalculationsGrid_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (!Vm.OpenSelectedForEditCommand.CanExecute(null))
            return;

        Vm.OpenSelectedForEditCommand.Execute(null);
    }

    private void MinuteTextBox_OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox textBox)
            textBox.SelectAll();
    }

    private void MinuteTextBox_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TextBox textBox) return;
        if (textBox.IsKeyboardFocusWithin) return;

        e.Handled = true;
        textBox.Focus();
    }

    private static bool TryExtractModelPath(IDataObject data, out string path)
    {
        path = string.Empty;

        if (data.GetDataPresent(DataFormats.FileDrop) && data.GetData(DataFormats.FileDrop) is string[] files)
        {
            var model = files.FirstOrDefault(f =>
                f.EndsWith(".3mf", StringComparison.OrdinalIgnoreCase) ||
                f.EndsWith(".gcode", StringComparison.OrdinalIgnoreCase) ||
                f.EndsWith(".gco", StringComparison.OrdinalIgnoreCase));
            if (model is not null)
            {
                path = model;
                return true;
            }
        }

        if (data.GetDataPresent(DataFormats.UnicodeText))
        {
            var text = data.GetData(DataFormats.UnicodeText) as string;
            if (!string.IsNullOrWhiteSpace(text))
            {
                var candidate = text.Trim().Trim('"');
                if (candidate.EndsWith(".3mf", StringComparison.OrdinalIgnoreCase) ||
                    candidate.EndsWith(".gcode", StringComparison.OrdinalIgnoreCase) ||
                    candidate.EndsWith(".gco", StringComparison.OrdinalIgnoreCase))
                {
                    path = candidate;
                    return true;
                }
            }
        }

        if (data.GetDataPresent(DataFormats.Text))
        {
            var text = data.GetData(DataFormats.Text) as string;
            if (!string.IsNullOrWhiteSpace(text))
            {
                var candidate = text.Trim().Trim('"');
                if (candidate.EndsWith(".3mf", StringComparison.OrdinalIgnoreCase) ||
                    candidate.EndsWith(".gcode", StringComparison.OrdinalIgnoreCase) ||
                    candidate.EndsWith(".gco", StringComparison.OrdinalIgnoreCase))
                {
                    path = candidate;
                    return true;
                }
            }
        }

        return false;
    }
}
