using SequentialRenumber.Infrastructure;
using SequentialRenumber.Revit;
using SequentialRenumber.UI;

namespace SequentialRenumber
{
    /// <summary>
    /// Entry point for the Sequential Renumber tool. Opens the modeless window as a
    /// singleton: running the command while the window is already open focuses the
    /// existing window instead of opening a second instance (spec section 4, rule 6).
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class Cmd_SequentialRenumberTool : IExternalCommand
    {
        private static SequentialRenumberWindow _window;

        /// <summary>
        /// Shows the singleton window and takes the startup snapshot: exactly one
        /// preselected element becomes the anchor (spec 7.1). This method is one of the
        /// two permitted Revit API contexts (spec section 4, rule 1).
        /// </summary>
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;

            if (_window != null)
            {
                _window.Activate();
                return Result.Succeeded;
            }

            try
            {
                var viewModel = new RenumberViewModel();
                var handler = new RenumberEventHandler(viewModel);
                ExternalEvent externalEvent = ExternalEvent.Create(handler);

                // Subscribes the session/document guards (spec section 4, rule 7) here in a
                // valid API context; the handler's Cleanup request unsubscribes them in one
                // final raise after the window closes.
                handler.Initialize(uiapp, externalEvent);

                _window = new SequentialRenumberWindow(
                    viewModel, handler, externalEvent, uiapp.MainWindowHandle);
                _window.Closed += (s, e) => _window = null;

                AdoptPreselection(uiapp, handler, viewModel);

                _window.Show();

                FileLogger.Info("Sequential Renumber window opened.");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                FileLogger.Error("Failed to open the Sequential Renumber window.", ex);
                message = ex.Message;
                _window = null;
                return Result.Failed;
            }
        }

        /// <summary>
        /// Startup snapshot: exactly one preselected element is adopted as the anchor; zero
        /// or many opens the empty state prompting for Pick New Element (spec 7.1).
        /// </summary>
        private static void AdoptPreselection(
            UIApplication uiapp, RenumberEventHandler handler, RenumberViewModel viewModel)
        {
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc?.Document;
            if (doc == null) return;

            ICollection<ElementId> selectedIds = uidoc.Selection.GetElementIds();
            if (selectedIds.Count == 1)
            {
                Element element = doc.GetElement(selectedIds.First());
                if (element != null && element.IsValidObject)
                {
                    handler.AdoptAnchor(doc, element);
                    return;
                }
            }

            viewModel.StatusText = selectedIds.Count > 1
                ? "More than one element was preselected. Press Pick New Element to choose a single anchor."
                : "Press Pick New Element to choose an anchor element.";
        }

        /// <summary>Defines the ribbon button for this command on the dev tab.</summary>
        internal static PushButtonData GetButtonData()
        {
            string buttonInternalName = "btn_SequentialRenumberTool";
            string buttonTitle = "Sequential Renumber";

            Common.ButtonDataClass myButtonData = new Common.ButtonDataClass(
                buttonInternalName,
                buttonTitle,
                MethodBase.GetCurrentMethod().DeclaringType?.FullName,
                Properties.Resources.Blue_32,
                Properties.Resources.Blue_16,
                "Sequentially renumber a text parameter across elements you click in order.");

            return myButtonData.Data;
        }
    }
}
