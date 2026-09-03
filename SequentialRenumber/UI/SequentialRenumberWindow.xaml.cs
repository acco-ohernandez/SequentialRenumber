using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using SequentialRenumber.Core;
using SequentialRenumber.Infrastructure;
using SequentialRenumber.Revit;

namespace SequentialRenumber.UI
{
    /// <summary>
    /// The modeless Sequential Renumber window. Owned by the Revit main window so it can
    /// never fall behind it. All Revit work is requested through <see cref="RenumberEventHandler"/>
    /// via the <see cref="ExternalEvent"/> created in the command; nothing here calls the API.
    /// </summary>
    internal partial class SequentialRenumberWindow : Window
    {
        private readonly RenumberEventHandler _handler;
        private ExternalEvent _externalEvent;
        private bool _askedAboutUnexportedRows;

        private RenumberViewModel Vm => (RenumberViewModel)DataContext;

        public SequentialRenumberWindow(
            RenumberViewModel viewModel,
            RenumberEventHandler handler,
            ExternalEvent externalEvent,
            IntPtr revitMainWindowHandle)
        {
            InitializeComponent();

            DataContext = viewModel;
            _handler = handler;
            _externalEvent = externalEvent;

            // Owner = Revit main window, so the modeless window stays on top of Revit
            // (spec section 4, rule 5).
            new WindowInteropHelper(this) { Owner = revitMainWindowHandle };
        }

        /// <summary>Begins the run (spec 7.4): anchor write, then the pick loop.</summary>
        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            RaiseRequest(RenumberRequest.StartRun);
        }

        /// <summary>Prompts for a new anchor element; the report is kept (spec 7.7).</summary>
        private void PickNewElementButton_Click(object sender, RoutedEventArgs e)
        {
            RaiseRequest(RenumberRequest.PickAnchor);
        }

        /// <summary>
        /// Close — the user is finished renumbering. Defensive guard: if a click ever lands
        /// while the pick loop is running (Revit normally disables the whole window in pick
        /// mode), ignore it — closing mid-loop would orphan the run.
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is RenumberViewModel vm && vm.IsRunActive) return;
            Close();
        }

        private void RaiseRequest(RenumberRequest request)
        {
            _handler.Request = request;
            _externalEvent?.Raise();
        }

        /// <summary>
        /// Selecting rows highlights their elements in the model (spec 7.6). Flagged
        /// (element-gone) rows are excluded before the request is raised.
        /// </summary>
        private void ReportGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Vm.IsRunActive) return;

            var rows = ReportGrid.SelectedItems.OfType<ReportRow>()
                .Where(r => !r.IsElementGone)
                .ToList();
            if (rows.Count == 0) return;

            _handler.PendingReportRows = rows;
            RaiseRequest(RenumberRequest.HighlightSelection);
        }

        private void SelectAllButton_Click(object sender, RoutedEventArgs e)
        {
            ReportGrid.Focus();
            ReportGrid.SelectAll();
        }

        /// <summary>Restores OldValue on every selected row's element — one undo step (spec 7.6).</summary>
        private void RevertSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            var rows = ReportGrid.SelectedItems.OfType<ReportRow>()
                .Where(r => !r.IsElementGone)
                .ToList();
            if (rows.Count == 0)
            {
                Vm.StatusText = "Select one or more report rows to revert.";
                return;
            }

            _handler.PendingReportRows = rows;
            RaiseRequest(RenumberRequest.RevertSelected);
        }

        /// <summary>Writes the full grid to a user-chosen CSV path (spec 7.6). Pure UI — no Revit API.</summary>
        private void ExportCsvButton_Click(object sender, RoutedEventArgs e)
        {
            if (Vm.ReportRows.Count == 0)
            {
                Vm.StatusText = "The report is empty; nothing to export.";
                return;
            }

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export report as CSV",
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                FileName = $"SequentialRenumber_Report_{DateTime.Now:yyyy-MM-dd_HHmm}.csv",
                DefaultExt = ".csv",
            };
            if (dialog.ShowDialog(this) != true) return;

            try
            {
                string csv = CsvExporter.Build(Vm.ReportRows.Select(r => r.Record));
                // UTF-8 with BOM so Excel picks the right encoding.
                File.WriteAllText(dialog.FileName, csv, new UTF8Encoding(true));
                Vm.MarkReportExported();
                Vm.StatusText = $"Report exported: {dialog.FileName}";
                FileLogger.Info($"Report exported to '{dialog.FileName}' ({Vm.ReportRows.Count} row(s)).");
            }
            catch (Exception ex)
            {
                FileLogger.Error("CSV export failed.", ex);
                Vm.StatusText = $"Export failed: {ex.Message}";
            }
        }

        /// <summary>Clears the grid after confirmation (spec 7.6). The run counter is not reset.</summary>
        private void ClearReportButton_Click(object sender, RoutedEventArgs e)
        {
            if (Vm.ReportRows.Count == 0) return;

            MessageBoxResult answer = MessageBox.Show(this,
                $"Clear all {Vm.ReportRows.Count} report row(s)? This does not undo any renames.",
                "Sequential Renumber", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes) return;

            Vm.ClearReport();
            Vm.StatusText = "Report cleared.";
        }

        /// <summary>Asks once about unexported report rows before closing (spec 7.7).</summary>
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (Vm.IsRunActive)
            {
                e.Cancel = true;
                return;
            }

            if (Vm.HasUnexportedRows && !_askedAboutUnexportedRows)
            {
                _askedAboutUnexportedRows = true;
                MessageBoxResult answer = MessageBox.Show(this,
                    "The report has rows that were not exported. Close anyway?",
                    "Sequential Renumber", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (answer != MessageBoxResult.Yes)
                {
                    e.Cancel = true;
                    return;
                }
            }

            base.OnClosing(e);
        }

        /// <summary>
        /// Unsubscribing Revit application events requires an API context, which a WPF
        /// Closed handler is not — so cleanup (unsubscribe + event disposal) is routed
        /// through one final external event raise instead of running here
        /// (spec section 4, rule 8).
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            if (_externalEvent != null)
            {
                _handler.Request = RenumberRequest.Cleanup;
                _externalEvent.Raise();
                _externalEvent = null;
            }

            FileLogger.Info("Window closed; cleanup requested via external event.");
            base.OnClosed(e);
        }
    }
}
