using Autodesk.Revit.UI.Selection;

namespace SequentialRenumber.Revit
{
    /// <summary>
    /// Abstraction over "get me the next element" so the run engine never depends on how
    /// picking works. Revit 2023 uses the PickObject loop; a SelectionChanged-based
    /// strategy can drop in behind a #if guard after the port without touching the engine
    /// (spec section 5).
    /// </summary>
    internal interface IPickStrategy
    {
        /// <summary>Returns the next picked ElementId, or null when the user cancels (Esc).</summary>
        ElementId PickNext(UIDocument uidoc, ISelectionFilter filter, string statusPrompt);
    }
}
