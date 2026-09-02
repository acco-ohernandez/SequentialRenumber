namespace SequentialRenumber.Core
{
    /// <summary>
    /// State of one run: the pattern, the counter, and the rollover-warning flag.
    /// Revit-free; the event handler owns element/document identity separately.
    /// </summary>
    public sealed class RenumberSession
    {
        private readonly ISequenceFormatter _formatter;
        private int _index;

        public RenumberSession(int runNumber, string prefix, string suffix, ISequenceFormatter formatter)
        {
            RunNumber = runNumber;
            Prefix = prefix ?? string.Empty;
            Suffix = suffix ?? string.Empty;
            _formatter = formatter;
        }

        public int RunNumber { get; }
        public string Prefix { get; }
        public string Suffix { get; }

        /// <summary>Set once the once-per-run rollover warning has been shown (spec 7.3).</summary>
        public bool RolloverWarned { get; set; }

        /// <summary>
        /// The counter portion of the next value (used for Seed auto-advance), or null when
        /// the sequence has hit the negative-step hard stop.
        /// </summary>
        public string PeekNextCore() => _formatter.ValueAt(_index);

        /// <summary>True when the next value has outgrown the seed's width.</summary>
        public bool PeekIsRollover() => _formatter.IsRolloverAt(_index);

        /// <summary>The full next value (prefix + counter + suffix), or null at the hard stop.</summary>
        public string ComposeNext()
        {
            string core = PeekNextCore();
            return core == null ? null : Prefix + core + Suffix;
        }

        /// <summary>Moves to the next value after a successful write. Skips never call this.</summary>
        public void Advance() => _index++;
    }
}
