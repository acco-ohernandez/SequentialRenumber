namespace SequentialRenumber.Core
{
    /// <summary>Outcome of one attempted rename. Duplicate outranks Overwrote (spec 7.5).</summary>
    public enum RenameStatus
    {
        Applied,
        Duplicate,
        Overwrote,
        Skipped,
        Failed,
        Reverted,
    }

    /// <summary>
    /// One logged rename in the session report. Stores the element id as a long — not an
    /// Element reference — so the record stays valid across model changes and survives the
    /// 2024+ port where ElementId values become 64-bit (spec section 6).
    /// </summary>
    public sealed class RenameRecord
    {
        public int RunNumber { get; set; }
        public DateTime TimestampLocal { get; set; }
        public long ElementIdValue { get; set; }
        public string CategoryName { get; set; }
        public string ParameterName { get; set; }
        public string OldValue { get; set; }
        public string NewValue { get; set; }
        public RenameStatus Status { get; set; }

        /// <summary>Failure reason, duplicate detail, overwrite note, etc.</summary>
        public string Note { get; set; }

        /// <summary>
        /// The key the value was written under, so Revert Selected can re-resolve the same
        /// parameter even after runs that targeted different parameters. Not exported to CSV.
        /// </summary>
        public TargetParameterKey ParameterKey { get; set; }
    }
}
