namespace SequentialRenumber.Revit
{
    /// <summary>
    /// Selects and zooms to report-row elements in the model (spec 7.6). Called only from
    /// the external event handler — SetElementIds/ShowElements are API calls.
    /// </summary>
    internal static class ElementHighlighter
    {
        /// <summary>Selects the elements and asks Revit to show them in a suitable view.</summary>
        public static void Highlight(UIDocument uidoc, ICollection<ElementId> elementIds)
        {
            uidoc.Selection.SetElementIds(elementIds);
            uidoc.ShowElements(elementIds);
        }
    }
}
