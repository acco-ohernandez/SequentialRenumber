using System.Globalization;

namespace SequentialRenumber.Core
{
    /// <summary>
    /// Digit seeds. Padding width is inferred from the seed's length (01 → 2, 001 → 3);
    /// rollover past the width is allowed (99 → 100); values below zero are never
    /// produced (negative-step hard stop).
    /// </summary>
    public sealed class NumericSequenceFormatter : ISequenceFormatter
    {
        private readonly long _seedValue;
        private readonly int _width;
        private readonly int _step;

        private NumericSequenceFormatter(long seedValue, int width, int step)
        {
            _seedValue = seedValue;
            _width = width;
            _step = step;
        }

        /// <summary>Parses a digits-only seed. Fails on seeds too long to compute safely.</summary>
        public static bool TryCreate(string seed, int step, out ISequenceFormatter formatter, out string error)
        {
            formatter = null;

            if (seed.Length > 18)
            {
                error = "Seed is too long (maximum 18 digits).";
                return false;
            }

            if (!long.TryParse(seed, NumberStyles.None, CultureInfo.InvariantCulture, out long seedValue))
            {
                error = "Seed must contain only digits.";
                return false;
            }

            formatter = new NumericSequenceFormatter(seedValue, seed.Length, step);
            error = null;
            return true;
        }

        /// <inheritdoc />
        public string ValueAt(int index)
        {
            long value = _seedValue + (long)index * _step;
            if (value < 0) return null;

            return value.ToString(CultureInfo.InvariantCulture).PadLeft(_width, '0');
        }

        /// <inheritdoc />
        public bool IsRolloverAt(int index)
        {
            long value = _seedValue + (long)index * _step;
            return value >= 0 && value.ToString(CultureInfo.InvariantCulture).Length > _width;
        }
    }
}
