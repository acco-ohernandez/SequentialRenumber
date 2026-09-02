using System.Windows;
using System.Windows.Interop;
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

        /// <summary>"Done" — the user is finished renumbering; closes the tool.</summary>
        private void DoneButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void RaiseRequest(RenumberRequest request)
        {
            _handler.Request = request;
            _externalEvent?.Raise();
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
