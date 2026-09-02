namespace SequentialRenumber.Core
{
    /// <summary>How the target parameter is re-resolved on each picked element (spec section 6).</summary>
    public enum ParameterKeyKind
    {
        /// <summary>Resolved by the BuiltInParameter enum value (stored as a long).</summary>
        BuiltIn,

        /// <summary>Resolved by the shared parameter GUID.</summary>
        Shared,

        /// <summary>Project parameter — resolved by definition name (the documented fallback risk).</summary>
        Project,
    }

    /// <summary>
    /// Identity of the parameter being written, plus what the dropdown shows. Display name
    /// alone is fragile across elements, so the resolution key (built-in id / shared GUID /
    /// name) is captured once from the anchor and reused for every pick. Revit-free by
    /// design: the built-in id is stored as a long, never as a BuiltInParameter.
    /// </summary>
    public sealed class TargetParameterKey
    {
        public ParameterKeyKind Kind { get; set; }

        /// <summary>The BuiltInParameter enum value when <see cref="Kind"/> is BuiltIn.</summary>
        public long BuiltInId { get; set; }

        /// <summary>The shared parameter GUID when <see cref="Kind"/> is Shared.</summary>
        public Guid SharedGuid { get; set; }

        /// <summary>The definition name; the resolution key when <see cref="Kind"/> is Project.</summary>
        public string Name { get; set; }

        /// <summary>Name shown in the dropdown.</summary>
        public string DisplayName { get; set; }

        /// <summary>The anchor element's current value, shown beside the name (spec 7.2).</summary>
        public string CurrentValue { get; set; }

        /// <summary>Dropdown line: name plus the anchor's current value when it has one.</summary>
        public string DisplayLabel =>
            string.IsNullOrEmpty(CurrentValue) ? DisplayName : $"{DisplayName}  —  \"{CurrentValue}\"";
    }
}
