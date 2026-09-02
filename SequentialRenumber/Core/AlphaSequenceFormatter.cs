using System.Text;

namespace SequentialRenumber.Core
{
    /// <summary>
    /// Letter seeds, Excel-column progression (bijective base-26: X, Y, Z, AA, AB, …).
    /// There is no zero digit, so there is no padding concept — seed AA simply means
    /// "start at the 27th value". Output case follows the seed's first character; values
    /// below A are never produced (negative-step hard stop).
    /// </summary>
    public sealed class AlphaSequenceFormatter : ISequenceFormatter
    {
        private readonly long _seedNumber;
        private readonly int _seedLength;
        private readonly bool _upperCase;
        private readonly int _step;

        private AlphaSequenceFormatter(long seedNumber, int seedLength, bool upperCase, int step)
        {
            _seedNumber = seedNumber;
            _seedLength = seedLength;
            _upperCase = upperCase;
            _step = step;
        }

        /// <summary>Parses a letters-only seed (case-insensitive; output case follows the seed).</summary>
        public static bool TryCreate(string seed, int step, out ISequenceFormatter formatter, out string error)
        {
            formatter = null;

            if (seed.Length > 10)
            {
                error = "Seed is too long (maximum 10 letters).";
                return false;
            }

            long number = 0;
            foreach (char c in seed)
            {
                char u = char.ToUpperInvariant(c);
                if (u < 'A' || u > 'Z')
                {
                    error = "Seed must contain only letters A-Z.";
                    return false;
                }
                number = number * 26 + (u - 'A' + 1);
            }

            formatter = new AlphaSequenceFormatter(number, seed.Length, char.IsUpper(seed[0]), step);
            error = null;
            return true;
        }

        /// <inheritdoc />
        public string ValueAt(int index)
        {
            long value = _seedNumber + (long)index * _step;
            if (value < 1) return null;

            return Format(value, _upperCase);
        }

        /// <inheritdoc />
        public bool IsRolloverAt(int index)
        {
            long value = _seedNumber + (long)index * _step;
            return value >= 1 && Format(value, _upperCase).Length > _seedLength;
        }

        private static string Format(long value, bool upperCase)
        {
            char baseChar = upperCase ? 'A' : 'a';
            var sb = new StringBuilder();
            while (value > 0)
            {
                value--;
                sb.Insert(0, (char)(baseChar + (int)(value % 26)));
                value /= 26;
            }
            return sb.ToString();
        }
    }
}
