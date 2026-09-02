using Autodesk.Revit.UI.Selection;

namespace SequentialRenumber.Revit
{
    /// <summary>
    /// Pick filter for the run. Linked models are never pickable — with ObjectType.Element
    /// a click on a link resolves to the RevitLinkInstance, so it is rejected here in
    /// AllowElement (spec 7.8). The category lock applies only when the user left
    /// "Restrict picking to anchor's category" checked.
    /// </summary>
    internal class CategorySelectionFilter : ISelectionFilter
    {
        private readonly ElementId _categoryId;

        /// <param name="categoryId">Category to lock picking to, or null to allow any category.</param>
        public CategorySelectionFilter(ElementId categoryId)
        {
            _categoryId = categoryId;
        }

        /// <summary>Rejects link instances always; enforces the category lock when one is set.</summary>
        public bool AllowElement(Element elem)
        {
            if (elem is RevitLinkInstance) return false;
            if (_categoryId == null) return true;

            return elem.Category != null && elem.Category.Id == _categoryId;
        }

        /// <summary>Backstop: nothing resolving into a link is ever a valid pick.</summary>
        public bool AllowReference(Reference reference, XYZ position) => false;
    }
}
