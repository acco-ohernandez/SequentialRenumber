namespace SequentialRenumber.Revit
{
    /// <summary>
    /// The one place ElementId converts to and from a long. Revit 2023 exposes the 32-bit
    /// <c>IntegerValue</c>; 2024+ replaces it with the 64-bit <c>Value</c> (and a long
    /// constructor). Every caller already works in longs, so this single guard covers the
    /// whole codebase (spec sections 3.2 and 6).
    /// </summary>
    internal static class RevitVersionCompat
    {
        /// <summary>Numeric value of an ElementId, widened to long for portability.</summary>
        public static long GetValue(ElementId id)
        {
#if REVIT2023
            return id.IntegerValue;
#else
            return id.Value;
#endif
        }

        /// <summary>ElementId from a stored long value.</summary>
        public static ElementId ToElementId(long value)
        {
#if REVIT2023
            return new ElementId((int)value);
#else
            return new ElementId(value);
#endif
        }
    }
}
