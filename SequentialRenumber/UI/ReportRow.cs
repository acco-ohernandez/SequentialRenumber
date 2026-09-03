using System.ComponentModel;
using SequentialRenumber.Core;

namespace SequentialRenumber.UI
{
    /// <summary>
    /// Grid-facing wrapper around one <see cref="RenameRecord"/>. The record itself stays a
    /// plain Core object; this wrapper raises change notifications when a revert updates the
    /// status or when the element turns out to no longer exist in the document.
    /// </summary>
    internal class ReportRow : INotifyPropertyChanged
    {
        private bool _isElementGone;

        public ReportRow(RenameRecord record)
        {
            Record = record;
        }

        /// <summary>The underlying report record (used for CSV export and revert).</summary>
        public RenameRecord Record { get; }

        public int RunNumber => Record.RunNumber;
        public string Time => Record.TimestampLocal.ToString("HH:mm:ss");
        public long ElementIdValue => Record.ElementIdValue;
        public string CategoryName => Record.CategoryName;
        public string ParameterName => Record.ParameterName;
        public string OldValue => Record.OldValue;
        public string NewValue => Record.NewValue;
        public string Status => Record.Status.ToString();
        public string Note => Record.Note;

        /// <summary>
        /// Set when a highlight or revert discovers the element no longer exists. Flagged
        /// rows are excluded from highlight and revert (spec 7.6).
        /// </summary>
        public bool IsElementGone
        {
            get => _isElementGone;
            set
            {
                if (_isElementGone == value) return;
                _isElementGone = value;
                OnPropertyChanged(nameof(IsElementGone));
            }
        }

        /// <summary>Re-raises Status/Note after the handler mutates the underlying record.</summary>
        public void RefreshFromRecord()
        {
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(Note));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
