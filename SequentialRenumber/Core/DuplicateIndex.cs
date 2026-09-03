namespace SequentialRenumber.Core
{
    /// <summary>
    /// In-memory, case-insensitive set of the target parameter's existing values, built once
    /// at run start (spec 7.5) and kept current as the run writes, reverts remove, and
    /// restored old values are re-added. Never queried against the model after the build —
    /// no collector runs per pick.
    /// </summary>
    public sealed class DuplicateIndex
    {
        private readonly HashSet<string> _values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Number of distinct values indexed (log/status detail).</summary>
        public int Count => _values.Count;

        /// <summary>True when the value already exists somewhere in the indexed scope.</summary>
        public bool Contains(string value) => !string.IsNullOrEmpty(value) && _values.Contains(value);

        /// <summary>Records a value as existing (initial build, and after every write).</summary>
        public void Add(string value)
        {
            if (!string.IsNullOrEmpty(value)) _values.Add(value);
        }

        /// <summary>Forgets a value (after a revert removes it from the model).</summary>
        public void Remove(string value)
        {
            if (!string.IsNullOrEmpty(value)) _values.Remove(value);
        }
    }
}
