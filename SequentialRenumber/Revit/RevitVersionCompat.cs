namespace SequentialRenumber.Revit
{
    /// <summary>
    /// The one place ElementId converts to and from a long. Revit 2023 exposes the 32-bit
    /// <c>IntegerValue</c>; 2024+ replaces it with the 64-bit <c>Value</c>. When this ports
    /// into BTT_ACCORevit-Ribbons, a single <c>#if REVIT2023</c> guard here covers every
    /// caller (spec sections 3.2 and 6).
    /// </summary>
    internal static class RevitVersionCompat
    {
        /// <summary>Numeric value of an ElementId, widened to long for portability.</summary>
        public static long GetValue(ElementId id) => id.IntegerValue;
        // 2024+: public static long GetValue(ElementId id) => id.Value;

        /// <summary>ElementId from a stored long value.</summary>
        public static ElementId ToElementId(long value) => new ElementId((int)value);
        // 2024+: public static ElementId ToElementId(long value) => new ElementId(value);
    }
}
