namespace SequentialRenumber.Core
{
    /// <summary>
    /// Computes the counter portion of the sequence (no prefix/suffix). Index 0 is the
    /// seed itself; each subsequent index applies the step once more. Implementations are
    /// pure and Revit-free so they stay unit testable and portable.
    /// </summary>
    public interface ISequenceFormatter
    {
        /// <summary>
        /// The formatted counter value at <paramref name="index"/>, or null when the value
        /// would fall below the sequence floor (numeric &lt; 0, alphabetic &lt; A) — the
        /// negative-step hard stop.
        /// </summary>
        string ValueAt(int index);

        /// <summary>
        /// True when the value at <paramref name="index"/> has outgrown the seed's width
        /// (numeric: more digits than the padding; alphabetic: more letters than the seed).
        /// Triggers the once-per-run status-bar warning.
        /// </summary>
        bool IsRolloverAt(int index);
    }

    /// <summary>
    /// Builds the right formatter from the Seed field: all digits → numeric, all letters →
    /// alphabetic, anything else → invalid. This is the single place the mode is detected.
    /// </summary>
    public static class SequenceFormatterFactory
    {
        /// <summary>
        /// Tries to build a formatter for the given seed and step. Returns false with a
        /// user-facing <paramref name="error"/> when the seed or step is invalid.
        /// </summary>
        public static bool TryCreate(string seed, int step, out ISequenceFormatter formatter, out string error)
        {
            formatter = null;

            if (string.IsNullOrEmpty(seed))
            {
                error = "Seed is required.";
                return false;
            }

            if (step == 0)
            {
                error = "Step must be non-zero.";
                return false;
            }

            bool allDigits = seed.All(c => c >= '0' && c <= '9');
            bool allLetters = seed.All(c => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'));

            if (allDigits)
            {
                return NumericSequenceFormatter.TryCreate(seed, step, out formatter, out error);
            }

            if (allLetters)
            {
                return AlphaSequenceFormatter.TryCreate(seed, step, out formatter, out error);
            }

            error = "Seed must be all digits (01) or all letters (A) — not mixed.";
            return false;
        }
    }
}
