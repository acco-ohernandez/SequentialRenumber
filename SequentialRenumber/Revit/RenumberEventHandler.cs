using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI.Events;
using SequentialRenumber.Core;
using SequentialRenumber.Infrastructure;
using SequentialRenumber.UI;

namespace SequentialRenumber.Revit
{
    /// <summary>
    /// The kinds of work the modeless window can ask Revit to do. The window sets one of
    /// these and raises the external event; it never touches the Revit API itself.
    /// </summary>
    internal enum RenumberRequest
    {
        None,

        /// <summary>Pick a new anchor element and rescan its parameters (spec 7.7).</summary>
        PickAnchor,

        /// <summary>Run the sequence: write the anchor, then the pick loop (spec 7.4).</summary>
        StartRun,

        /// <summary>
        /// Final raise after the window closes: unsubscribe the application events and
        /// dispose the external event. Unsubscribing Revit events requires an API context,
        /// so this cannot happen in the WPF Closed handler (spec section 4, rule 8).
        /// </summary>
        Cleanup,
    }

    /// <summary>
    /// The only Revit API entry point besides the command's startup snapshot (spec section 4,
    /// rule 1). Owns the session's document/anchor identity, the run loop, and the report
    /// records. The window communicates by setting <see cref="Request"/> and raising the
    /// <see cref="ExternalEvent"/> that wraps this handler.
    /// </summary>
    internal class RenumberEventHandler : IExternalEventHandler
    {
        private const string RunPrompt = "Sequential Renumber: click the next element. Press Esc to stop.";
        private const string RunningStatus = "Running. Click elements in order. Press Esc to stop.";

        private readonly RenumberViewModel _viewModel;
        private readonly IPickStrategy _pickStrategy = new PickObjectLoopStrategy();

        // Session identity. Never an Element reference — the anchor is stored as a long id
        // and re-resolved through doc.GetElement on every use (spec section 4, rule 4).
        private Document _sessionDoc;
        private long _anchorIdValue;

        private int _runNumber;

        // Set by Initialize; cleared again by the Cleanup request.
        private UIApplication _uiApplication;
        private ExternalEvent _ownedEvent;

        public RenumberEventHandler(RenumberViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        /// <summary>
        /// Called from the command (a valid API context): subscribes the session/document
        /// guards and takes ownership of the external event so Cleanup can dispose it.
        /// </summary>
        public void Initialize(UIApplication uiapp, ExternalEvent ownedEvent)
        {
            _uiApplication = uiapp;
            _ownedEvent = ownedEvent;
            uiapp.ViewActivated += OnViewActivated;
            uiapp.Application.DocumentClosing += OnDocumentClosing;
        }

        /// <summary>The work to perform on the next <see cref="Execute"/>; consumed once per raise.</summary>
        public RenumberRequest Request { get; set; } = RenumberRequest.None;

        /// <summary>Session report, accumulating across runs. The Phase 3 grid binds to this.</summary>
        public List<RenameRecord> Records { get; } = new List<RenameRecord>();

        /// <summary>
        /// Runs in a valid Revit API context. Dispatches the pending request and always
        /// resets it so a stray re-raise cannot repeat the previous action.
        /// </summary>
        public void Execute(UIApplication app)
        {
            RenumberRequest request = Request;
            Request = RenumberRequest.None;

            try
            {
                switch (request)
                {
                    case RenumberRequest.PickAnchor:
                        ExecutePickAnchor(app);
                        break;
                    case RenumberRequest.StartRun:
                        ExecuteStartRun(app);
                        break;
                    case RenumberRequest.Cleanup:
                        ExecuteCleanup();
                        break;
                }
            }
            catch (Exception ex)
            {
                FileLogger.Error($"Unhandled exception executing request '{request}'.", ex);
                _viewModel.StatusText = $"Error: {ex.Message}";
                _viewModel.IsRunActive = false;
            }
        }

        /// <summary>Name shown by Revit in its event diagnostics.</summary>
        public string GetName() => "SequentialRenumber.RenumberEventHandler";

        /// <summary>
        /// Adopts an element as the session anchor and populates the parameter dropdown.
        /// Called from the command's startup snapshot (preselection) and from PickAnchor —
        /// both valid API contexts.
        /// </summary>
        public void AdoptAnchor(Document doc, Element element)
        {
            _sessionDoc = doc;
            _anchorIdValue = RevitVersionCompat.GetValue(element.Id);

            string categoryName = element.Category?.Name ?? "(no category)";
            string anchorText = $"Anchor: {categoryName} — {element.Name} (Id {_anchorIdValue})";

            List<TargetParameterKey> parameters = ParameterScanner.Scan(element);
            _viewModel.SetAnchor(anchorText, parameters);
            _viewModel.IsSessionDocActive = true;

            if (parameters.Count == 0)
            {
                _viewModel.StatusText = "This element has no writable text instance parameters. Pick a different element.";
                FileLogger.Warn($"Anchor {_anchorIdValue} ({categoryName}) has no eligible parameters.");
            }
            else
            {
                _viewModel.StatusText = $"Anchor set. Choose a parameter ({parameters.Count} eligible) and a pattern, then press Start.";
                FileLogger.Info($"Anchor adopted: id {_anchorIdValue}, category '{categoryName}', {parameters.Count} eligible parameter(s).");
            }
        }

        /// <summary>Ends the session when its document goes away (spec section 4, rule 7).</summary>
        public void OnDocumentClosing(object sender, DocumentClosingEventArgs e)
        {
            if (_sessionDoc == null || !e.Document.Equals(_sessionDoc)) return;

            _sessionDoc = null;
            _anchorIdValue = 0;
            _viewModel.ClearAnchor("The session document was closed. Pick a new element to start over.");
            FileLogger.Info("Session document closing; session cleared.");
        }

        /// <summary>Disables Start while a different document is active (spec section 4, rule 7).</summary>
        public void OnViewActivated(object sender, ViewActivatedEventArgs e)
        {
            if (_sessionDoc == null) return;

            bool sameDoc = e.Document != null && e.Document.Equals(_sessionDoc);
            if (_viewModel.IsSessionDocActive == sameDoc) return;

            _viewModel.IsSessionDocActive = sameDoc;
            _viewModel.StatusText = sameDoc
                ? "Back in the session document. Ready."
                : "A different document is active. Switch back, or pick a new element in this document.";
        }

        /// <summary>
        /// Runs in a valid API context on the final raise after the window closed:
        /// unsubscribes the application events and disposes the external event.
        /// </summary>
        private void ExecuteCleanup()
        {
            if (_uiApplication != null)
            {
                _uiApplication.ViewActivated -= OnViewActivated;
                _uiApplication.Application.DocumentClosing -= OnDocumentClosing;
                _uiApplication = null;
            }

            _ownedEvent?.Dispose();
            _ownedEvent = null;

            FileLogger.Info("Cleanup executed: application events unsubscribed, external event disposed.");
        }

        private void ExecutePickAnchor(UIDocument uidoc, Document doc)
        {
            _viewModel.StatusText = "Pick an anchor element in the model. Press Esc to cancel.";

            ElementId pickedId = _pickStrategy.PickNext(
                uidoc, new CategorySelectionFilter(null), "Sequential Renumber: pick the anchor element. Press Esc to cancel.");

            if (pickedId == null)
            {
                _viewModel.StatusText = "Anchor pick cancelled.";
                return;
            }

            Element element = doc.GetElement(pickedId);
            if (element == null || !element.IsValidObject)
            {
                _viewModel.StatusText = "The picked element could not be read. Try again.";
                return;
            }

            AdoptAnchor(doc, element);
        }

        private void ExecutePickAnchor(UIApplication app)
        {
            UIDocument uidoc = app.ActiveUIDocument;
            Document doc = uidoc?.Document;
            if (doc == null)
            {
                _viewModel.StatusText = "No active document.";
                return;
            }

            ExecutePickAnchor(uidoc, doc);
        }

        private void ExecuteStartRun(UIApplication app)
        {
            UIDocument uidoc = app.ActiveUIDocument;
            Document doc = uidoc?.Document;

            if (doc == null || _sessionDoc == null || !doc.Equals(_sessionDoc))
            {
                _viewModel.StatusText = "The session document is not active. Switch back to it, or pick a new element.";
                return;
            }

            Element anchor = doc.GetElement(RevitVersionCompat.ToElementId(_anchorIdValue));
            if (anchor == null || !anchor.IsValidObject)
            {
                _viewModel.ClearAnchor("The anchor element no longer exists. Pick a new element.");
                return;
            }

            TargetParameterKey parameterKey = _viewModel.SelectedParameter;
            if (parameterKey == null) return;

            if (!int.TryParse(_viewModel.StepText, out int step))
            {
                _viewModel.StatusText = "Pattern invalid: Step must be an integer.";
                return;
            }

            if (!SequenceFormatterFactory.TryCreate(_viewModel.Seed, step, out ISequenceFormatter formatter, out string patternError))
            {
                _viewModel.StatusText = $"Pattern invalid: {patternError}";
                return;
            }

            _runNumber++;
            var session = new RenumberSession(_runNumber, _viewModel.Prefix, _viewModel.Suffix, formatter);

            bool restrict = _viewModel.RestrictToCategory;
            var filter = new CategorySelectionFilter(restrict ? anchor.Category?.Id : null);

            _viewModel.IsRunActive = true;
            int written = 0;

            FileLogger.Info(
                $"Run {_runNumber} started. Parameter '{parameterKey.DisplayName}' ({parameterKey.Kind}), " +
                $"pattern '{_viewModel.Prefix}|{_viewModel.Seed}|{step}|{_viewModel.Suffix}', restrict={restrict}.");

            try
            {
                using (var group = new TransactionGroup(doc, $"Sequential Renumber (Run {_runNumber})"))
                {
                    group.Start();

                    // The anchor gets value #1 automatically (spec 7.4, decision A).
                    ProcessResult anchorResult = ProcessElement(doc, anchor, session, parameterKey, ref written);

                    if (anchorResult != ProcessResult.StopRun)
                    {
                        while (true)
                        {
                            _viewModel.StatusText = $"{RunningStatus} Written: {written}.";

                            ElementId pickedId = _pickStrategy.PickNext(uidoc, filter, RunPrompt);
                            if (pickedId == null) break; // Esc

                            Element element = doc.GetElement(pickedId);
                            if (element == null || !element.IsValidObject)
                            {
                                AddRecord(session, 0, "(unknown)", parameterKey.DisplayName, string.Empty, string.Empty,
                                    RenameStatus.Skipped, "Element could not be read after picking.");
                                continue;
                            }

                            if (ProcessElement(doc, element, session, parameterKey, ref written) == ProcessResult.StopRun)
                            {
                                break;
                            }
                        }
                    }

                    // One undo step for the whole run (spec 7.4).
                    group.Assimilate();
                }
            }
            finally
            {
                _viewModel.IsRunActive = false;
            }

            // Seed auto-advance so a re-Start continues the sequence (spec 7.3, decision B).
            string nextCore = session.PeekNextCore();
            if (nextCore != null)
            {
                _viewModel.Seed = nextCore;
            }

            _viewModel.StatusText = $"Run {_runNumber} ended. {written} value(s) written. One Ctrl+Z undoes the whole run.";
            FileLogger.Info($"Run {_runNumber} ended. {written} value(s) written.");
        }

        private enum ProcessResult { Continue, StopRun }

        /// <summary>
        /// Renames one element: re-resolve the parameter, compute the next value, write it in
        /// its own transaction. A failure rolls back that transaction only and the loop keeps
        /// going — one bad element must not kill a 60 element run (spec 7.4).
        /// </summary>
        private ProcessResult ProcessElement(
            Document doc, Element element, RenumberSession session, TargetParameterKey parameterKey, ref int written)
        {
            long elementIdValue = RevitVersionCompat.GetValue(element.Id);
            string categoryName = element.Category?.Name ?? "(no category)";

            Parameter parameter = ParameterScanner.Resolve(element, parameterKey);
            if (parameter == null)
            {
                AddRecord(session, elementIdValue, categoryName, parameterKey.DisplayName, string.Empty, string.Empty,
                    RenameStatus.Skipped, "Parameter not present or not writable on this element.");
                _viewModel.StatusText = $"{RunningStatus} Skipped element {elementIdValue} (parameter not present). Written: {written}.";
                return ProcessResult.Continue;
            }

            string newValue = session.ComposeNext();
            if (newValue == null)
            {
                // Negative-step hard stop (spec 7.3, decision D).
                _viewModel.StatusText = "Sequence reached its lower limit; the run stopped.";
                FileLogger.Warn($"Run {session.RunNumber}: negative-step hard stop reached.");
                return ProcessResult.StopRun;
            }

            if (session.PeekIsRollover() && !session.RolloverWarned)
            {
                session.RolloverWarned = true;
                _viewModel.StatusText = $"Note: values have rolled past the seed's width (now {newValue}).";
                FileLogger.Warn($"Run {session.RunNumber}: padding rollover at '{newValue}'.");
            }

            string oldValue = parameter.AsString() ?? string.Empty;

            using (var transaction = new Transaction(doc, $"Renumber {newValue}"))
            {
                try
                {
                    transaction.Start();
                    parameter.Set(newValue);

                    if (transaction.Commit() != TransactionStatus.Committed)
                    {
                        transaction.RollBack();
                        AddRecord(session, elementIdValue, categoryName, parameterKey.DisplayName, oldValue, newValue,
                            RenameStatus.Failed, "Transaction did not commit.");
                        return ProcessResult.Continue;
                    }
                }
                catch (Exception ex)
                {
                    if (transaction.GetStatus() == TransactionStatus.Started)
                    {
                        transaction.RollBack();
                    }
                    AddRecord(session, elementIdValue, categoryName, parameterKey.DisplayName, oldValue, newValue,
                        RenameStatus.Failed, ex.Message);
                    FileLogger.Error($"Run {session.RunNumber}: write of '{newValue}' to element {elementIdValue} failed.", ex);
                    _viewModel.StatusText = $"{RunningStatus} Failed on element {elementIdValue}. Written: {written}.";
                    return ProcessResult.Continue;
                }
            }

            AddRecord(session, elementIdValue, categoryName, parameterKey.DisplayName, oldValue, newValue,
                RenameStatus.Applied, string.Empty);
            session.Advance();
            written++;
            _viewModel.StatusText = $"{RunningStatus} Wrote {newValue}. Written: {written}.";
            return ProcessResult.Continue;
        }

        private void AddRecord(RenumberSession session, long elementIdValue, string categoryName,
            string parameterName, string oldValue, string newValue, RenameStatus status, string note)
        {
            Records.Add(new RenameRecord
            {
                RunNumber = session.RunNumber,
                TimestampLocal = DateTime.Now,
                ElementIdValue = elementIdValue,
                CategoryName = categoryName,
                ParameterName = parameterName,
                OldValue = oldValue,
                NewValue = newValue,
                Status = status,
                Note = note,
            });

            FileLogger.Info(
                $"Run {session.RunNumber} | {status} | element {elementIdValue} | '{oldValue}' -> '{newValue}'" +
                (string.IsNullOrEmpty(note) ? string.Empty : $" | {note}"));
        }
    }
}
