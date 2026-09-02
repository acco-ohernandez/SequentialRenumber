using Autodesk.Revit.UI.Selection;

namespace SequentialRenumber.Revit
{
    /// <summary>
    /// Revit 2023 pick implementation: one PickObject call per element. Escape surfaces as
    /// Autodesk.Revit.Exceptions.OperationCanceledException (never the System one — spec
    /// section 4, rule 3) and is translated to null so the run loop ends cleanly.
    /// </summary>
    internal class PickObjectLoopStrategy : IPickStrategy
    {
        /// <inheritdoc />
        public ElementId PickNext(UIDocument uidoc, ISelectionFilter filter, string statusPrompt)
        {
            try
            {
                Reference reference = uidoc.Selection.PickObject(ObjectType.Element, filter, statusPrompt);
                return reference?.ElementId;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return null;
            }
        }
    }
}
