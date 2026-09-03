using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SequentialRenumber.Core;

namespace SequentialRenumber.UI
{
    /// <summary>
    /// Binding surface for <see cref="SequentialRenumberWindow"/>: anchor state, parameter
    /// dropdown, pattern fields with live preview/validation, the restrict-category option,
    /// and run state. Revit-free; the event handler pushes state in and reads it back.
    /// </summary>
    internal class RenumberViewModel : INotifyPropertyChanged
    {
        private string _statusText = "Ready.";
        private bool _hasAnchor;
        private string _anchorText = "No anchor element. Press Pick New Element, or preselect one element before launching.";
        private TargetParameterKey _selectedParameter;
        private string _prefix = string.Empty;
        private string _seed = "01";
        private string _stepText = "1";
        private string _suffix = string.Empty;
        private string _previewText = string.Empty;
        private string _patternError = string.Empty;
        private bool _restrictToCategory = true;
        private bool _promptOnDuplicates;
        private bool _isRunActive;
        private bool _isSessionDocActive = true;
        private bool _hasUnexportedRows;

        public RenumberViewModel()
        {
            UpdatePattern();
        }

        /// <summary>Text shown in the window's status bar.</summary>
        public string StatusText
        {
            get => _statusText;
            set => SetField(ref _statusText, value);
        }

        /// <summary>True once an anchor element has been adopted (preselection or pick).</summary>
        public bool HasAnchor
        {
            get => _hasAnchor;
            private set
            {
                if (SetField(ref _hasAnchor, value))
                {
                    OnPropertyChanged(nameof(CanStart));
                    OnPropertyChanged(nameof(CanEditInputs));
                }
            }
        }

        /// <summary>Describes the anchor element (category, name, id) or the empty-state prompt.</summary>
        public string AnchorText
        {
            get => _anchorText;
            private set => SetField(ref _anchorText, value);
        }

        /// <summary>Eligible text parameters scanned from the anchor (spec 7.2).</summary>
        public ObservableCollection<TargetParameterKey> ParameterOptions { get; } =
            new ObservableCollection<TargetParameterKey>();

        /// <summary>
        /// The parameter the run will write. Start stays disabled until one is chosen.
        /// Selecting a parameter prefills Prefix with its current value so the user trims
        /// instead of typing from scratch.
        /// </summary>
        public TargetParameterKey SelectedParameter
        {
            get => _selectedParameter;
            set
            {
                if (SetField(ref _selectedParameter, value))
                {
                    if (value != null)
                    {
                        Prefix = value.CurrentValue ?? string.Empty;
                    }
                    OnPropertyChanged(nameof(CanStart));
                }
            }
        }

        /// <summary>Optional free-text prefix.</summary>
        public string Prefix
        {
            get => _prefix;
            set { if (SetField(ref _prefix, value)) UpdatePattern(); }
        }

        /// <summary>
        /// Required. All digits → numeric mode (padding from length); all letters →
        /// alphabetic mode (Excel-style). Auto-advanced by the handler when a run ends.
        /// </summary>
        public string Seed
        {
            get => _seed;
            set { if (SetField(ref _seed, value)) UpdatePattern(); }
        }

        /// <summary>Step as typed; validated as a non-zero integer (may be negative).</summary>
        public string StepText
        {
            get => _stepText;
            set { if (SetField(ref _stepText, value)) UpdatePattern(); }
        }

        /// <summary>Optional free-text suffix.</summary>
        public string Suffix
        {
            get => _suffix;
            set { if (SetField(ref _suffix, value)) UpdatePattern(); }
        }

        /// <summary>Live preview: "Next value: EQ-01-A" (spec 7.3).</summary>
        public string PreviewText
        {
            get => _previewText;
            private set => SetField(ref _previewText, value);
        }

        /// <summary>Why the pattern is invalid; empty when it is valid.</summary>
        public string PatternError
        {
            get => _patternError;
            private set => SetField(ref _patternError, value);
        }

        /// <summary>True when the pattern currently parses (Start gate).</summary>
        public bool IsPatternValid => string.IsNullOrEmpty(_patternError);

        /// <summary>Locks picking to the anchor's category (spec 7.4, default checked).</summary>
        public bool RestrictToCategory
        {
            get => _restrictToCategory;
            set => SetField(ref _restrictToCategory, value);
        }

        /// <summary>
        /// When on, a duplicate value shows a Skip / Write Anyway / Stop Run prompt instead
        /// of being written silently (spec 7.5, default off).
        /// </summary>
        public bool PromptOnDuplicates
        {
            get => _promptOnDuplicates;
            set => SetField(ref _promptOnDuplicates, value);
        }

        /// <summary>The session report, accumulating across runs (spec 7.6).</summary>
        public ObservableCollection<ReportRow> ReportRows { get; } = new ObservableCollection<ReportRow>();

        /// <summary>True when rows were added since the last CSV export (Close asks once — spec 7.7).</summary>
        public bool HasUnexportedRows
        {
            get => _hasUnexportedRows;
            private set => SetField(ref _hasUnexportedRows, value);
        }

        /// <summary>Appends one record to the report grid. Called by the handler as it logs.</summary>
        public void AddReportRow(Core.RenameRecord record)
        {
            ReportRows.Add(new ReportRow(record));
            HasUnexportedRows = true;
        }

        /// <summary>Marks the current report content as exported.</summary>
        public void MarkReportExported() => HasUnexportedRows = false;

        /// <summary>Empties the report grid (run counter is NOT reset — spec 7.6).</summary>
        public void ClearReport()
        {
            ReportRows.Clear();
            HasUnexportedRows = false;
        }

        /// <summary>True while the pick loop is running; locks the inputs and the checkbox.</summary>
        public bool IsRunActive
        {
            get => _isRunActive;
            set
            {
                if (SetField(ref _isRunActive, value))
                {
                    OnPropertyChanged(nameof(CanStart));
                    OnPropertyChanged(nameof(CanEditInputs));
                    OnPropertyChanged(nameof(CanToggleRestrict));
                    OnPropertyChanged(nameof(StartButtonText));
                }
            }
        }

        /// <summary>False when a different document is active than the session's (spec section 4, rule 7).</summary>
        public bool IsSessionDocActive
        {
            get => _isSessionDocActive;
            set
            {
                if (SetField(ref _isSessionDocActive, value))
                {
                    OnPropertyChanged(nameof(CanStart));
                }
            }
        }

        /// <summary>Start button label: reminds the user how to stop while the run is active.</summary>
        public string StartButtonText => IsRunActive ? "Esc to stop" : "Start";

        /// <summary>Start gate: anchor + parameter + valid pattern + correct document + idle (spec 7.7).</summary>
        public bool CanStart =>
            HasAnchor && SelectedParameter != null && IsPatternValid && IsSessionDocActive && !IsRunActive;

        /// <summary>Pattern/parameter inputs are editable once an anchor exists and no run is active.</summary>
        public bool CanEditInputs => HasAnchor && !IsRunActive;

        /// <summary>The restrict checkbox is locked while a run is active (spec 7.4).</summary>
        public bool CanToggleRestrict => !IsRunActive;

        /// <summary>Adopts a new anchor: replaces the dropdown contents and clears the selection.</summary>
        public void SetAnchor(string anchorText, IEnumerable<TargetParameterKey> parameters)
        {
            AnchorText = anchorText;
            ParameterOptions.Clear();
            foreach (TargetParameterKey key in parameters)
            {
                ParameterOptions.Add(key);
            }
            SelectedParameter = null;
            HasAnchor = true;
        }

        /// <summary>Drops the anchor (document closed, anchor deleted) and explains why.</summary>
        public void ClearAnchor(string statusMessage)
        {
            ParameterOptions.Clear();
            SelectedParameter = null;
            HasAnchor = false;
            AnchorText = "No anchor element. Press Pick New Element.";
            StatusText = statusMessage;
        }

        private void UpdatePattern()
        {
            if (!int.TryParse(_stepText, out int step))
            {
                PatternError = "Step must be an integer.";
            }
            else if (!SequenceFormatterFactory.TryCreate(_seed, step, out ISequenceFormatter formatter, out string error))
            {
                PatternError = error;
            }
            else
            {
                PatternError = string.Empty;
                PreviewText = $"New value: {_prefix}{formatter.ValueAt(0)}{_suffix}";
            }

            if (!IsPatternValid)
            {
                PreviewText = string.Empty;
            }

            OnPropertyChanged(nameof(IsPatternValid));
            OnPropertyChanged(nameof(CanStart));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
