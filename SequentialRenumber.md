# SequentialRenumber — Revit Add-in Specification

**Audience:** Claude Code, working inside `C:\Visual Studio Files\SequentialRenumber\`
**Status:** New build, dev sandbox only. Do not touch `BTT_ACCORevit-Ribbons`.

---

## 0. Working agreement

Read this entire document before writing anything.

1. Work in **phases** (Section 8). Do not write Phase 2 or Phase 3 code until Phase 1 builds, loads in Revit 2023, and I have confirmed it.
2. At the end of each phase, stop, summarize what you built, and wait for confirmation.
3. If a requirement here is ambiguous or conflicts with the Revit API, **ask instead of guessing**. Do not invent behavior for anything listed as out of scope.
4. Do not add NuGet packages beyond Section 2 without asking.
5. Every Revit API method you call must be one you can point to in the 2023 API. If you are unsure a member exists in Revit 2023, say so rather than writing it.

---

## 1. Purpose

A modeless Revit tool for sequential renumbering of a single **text (string) instance parameter** across elements the user clicks in order. It replaces manual typing of values like `EQ-01`, `EQ-02`, `EQ-03` (or `EQ-A`, `EQ-B`, `EQ-C`) into a schedule or properties palette.

Core loop: user picks a starting element, chooses which text parameter to write, sets a Prefix / Seed / Step / Suffix pattern, Preview Label, presses Start, then clicks elements in the model one after another. Each click writes the next value in the sequence and logs the change to a report the user can review, select from, and revert.

---

## 2. Environment

**Dev sandbox (build for this only):**

| Item | Value |
| --- | --- |
| Solution | `C:\Visual Studio Files\SequentialRenumber\SequentialRenumber.sln` |
| Project | `SequentialRenumber` |
| Revit version | **2023 only** |
| Target framework | `net48` |
| UI | WPF |
| Revit API | `Revit_All_Main_Versions_API_x64` NuGet package |
| Command entry point | `SequentialRenumber\Cmd_SequentialRenumberTool.cs` |

Note: the class and file are `Cmd_SequentialRenumberTool`.

Also produce a `SequentialRenumber.addin` manifest and a temporary dev ribbon tab named **`Dev Tools`** with a single button, `Sequential Renumber`, so the command can be launched in Revit 2023 without touching the production ribbons.

---

## 3. Port-forward constraints

This tool will later be moved into `BTT_ACCORevit-Ribbons`, which multi-targets **Revit 2023, 2024, 2025, 2026, 2027** across `net48` (2023/2024), `net8.0-windows` (2025/2026), and `net10.0-windows` (2027). Build the sandbox so that port is close to copy and paste.

Rules:

1. **Write version-agnostic code.** Do not use any API introduced after Revit 2023. Do **not** use `UIApplication.SelectionChanged` regardless of which version introduced it — it fires outside a valid API context and would need its own external-event choreography; `PickObject` is the v1 strategy.
2. **Isolate anything that might diverge by version** behind an interface or a single small class, so a `#if REVIT20xx` guard can be added later in one place rather than scattered through the code.
3. **Follow production naming:** command classes are `Cmd_<Name>`.
4. **Follow the production UI pattern:** modeless singleton window + `ExternalEvent` / `IExternalEventHandler`. Never call the Revit API directly from a WPF event handler.
5. **No `System.Windows.Forms`.** WPF only. Nothing that will not survive the move to `net8.0-windows` and `net10.0-windows`.
6. **No `Newtonsoft.Json` or third party dependencies.** CSV export is manual string building.
7. Keep business logic (number formatting, duplicate checking, session state) in plain classes with **no Revit API references**, so it is unit testable and portable.

---

## 4. Non-negotiable Revit API rules

1. Revit API access happens in exactly **two places**, both valid API contexts: `Cmd_SequentialRenumberTool.Execute` (startup snapshot only — read the preselection, scan the anchor element's parameters) and `RenumberEventHandler.Execute` (everything else). No other code touches the API.
2. `UIDocument.Selection.PickObject` is called **from inside the external event handler**, which is a valid API context.
3. Catch `Autodesk.Revit.Exceptions.OperationCanceledException` explicitly (not `System.OperationCanceledException`) to detect the user pressing Escape.
4. **Never store `Element` references.** Store `ElementId` plus enough document identity to validate later. Re-resolve elements with `doc.GetElement(id)` and check `IsValidObject` every time.
5. The modeless window must set its owner to the Revit main window handle via `WindowInteropHelper` and `UIApplication.MainWindowHandle` (available since 2019, version-stable through 2027). Do **not** use `Process.GetCurrentProcess().MainWindowHandle` — it is unreliable when Revit's main window is not the process's current main window. Without a correct owner the window falls behind Revit and users assume it crashed.
6. **Singleton window.** If the command runs while the window is already open, focus the existing window and return. Do not open a second instance.
7. Subscribe to `Application.DocumentClosing` and `UIApplication.ViewActivated`. If the session document is closed or the user switches to a **different document**, end the active run, disable Start, and show a clear status message. View switches within the same document are ignored. Do not silently write to the wrong document.
8. On window close: unregister the external event and unsubscribe all event handlers — **via one final external-event raise (a Cleanup request), never from the WPF Closed handler**: unsubscribing Revit application events outside an API context throws `InvalidOperationException` ("Revit is currently not within an API context" — hit and fixed 2026-09-02). (A `TransactionGroup` cannot survive outside a single `Execute` call, so none can be open while the window is clickable; keep only a defensive assimilate-if-open check inside the handler.)

---

## 5. Architecture

```
SequentialRenumber\
  Cmd_SequentialRenumberTool.cs          IExternalCommand entry point, singleton guard
  App_SequentialRenumber.cs              IExternalApplication, builds the Dev Tools ribbon tab
  SequentialRenumber.addin

  UI\
    SequentialRenumberWindow.xaml(.cs)   Modeless WPF window
    RenumberViewModel.cs                 Binding surface, INotifyPropertyChanged

  Revit\
    RenumberEventHandler.cs              IExternalEventHandler, the only API entry point
    IPickStrategy.cs                     Abstraction over "get me the next element"
    PickObjectLoopStrategy.cs            Revit 2023 compatible implementation
    CategorySelectionFilter.cs           ISelectionFilter; always rejects link instances, optionally locks to one category
    ParameterScanner.cs                  Discovers eligible text parameters on an element
    ElementHighlighter.cs                SetElementIds + ShowElements for report rows
    RevitVersionCompat.cs                ElementId <-> long conversion, isolated for the 2024+ port

  Core\                                  No Revit API references in this folder
    ISequenceFormatter.cs                Next-value computation contract (Prefix/Seed/Step/Suffix)
    NumericSequenceFormatter.cs          Digit seeds: padding, rollover
    AlphaSequenceFormatter.cs            Letter seeds: Excel-style bijective base-26
    RenumberSession.cs                   Run state, counter, pattern, target parameter
    RenameRecord.cs                      One logged rename
    DuplicateIndex.cs                    In-memory set of existing values
    CsvExporter.cs

  Infrastructure\
    FileLogger.cs
```

### IPickStrategy

```csharp
public interface IPickStrategy
{
    // Returns the next picked ElementId, or null when the user cancels.
    ElementId PickNext(UIDocument uidoc, ISelectionFilter filter, string statusPrompt);
}
```

`PickObjectLoopStrategy` wraps `PickObject` and returns null on `OperationCanceledException`. When the tool eventually ships for 2024+, a `SelectionChangedStrategy` can be dropped in behind a `#if` guard without touching the engine.

**Known limitation, document it in the UI:** while a run is active, Revit is in pick mode and the modeless window is not clickable. The status bar must read `Running. Click elements in order. Press Esc to stop.` Escape returns control to the window.

---

## 6. Data model

```csharp
public sealed class RenameRecord
{
    public int RunNumber { get; set; }
    public DateTime TimestampLocal { get; set; }
    public long ElementIdValue { get; set; }
    public string CategoryName { get; set; }
    public string ParameterName { get; set; }
    public string OldValue { get; set; }
    public string NewValue { get; set; }
    public RenameStatus Status { get; set; }   // Applied, Duplicate, Overwrote, Skipped, Failed, Reverted
    public string Note { get; set; }           // failure reason, duplicate detail, etc.
}
```

**`ElementIdValue` is `long` on purpose:** Revit 2023 exposes `ElementId.IntegerValue` (`int`); 2024+ replaces it with `ElementId.Value` (`long`). All conversion between `ElementId` and `long` goes through `RevitVersionCompat` so the eventual `#if` guard touches one file.

**Parameter identity:** matching by display name alone is fragile across elements. Resolve the target parameter by `BuiltInParameter` when it is a built-in, by shared parameter `GUID` when it is shared, and fall back to `Definition.Name` only for project parameters. Store whichever key was used on the session so every subsequent pick resolves the same parameter.

---

## 7. Functional specification

### 7.1 Startup

- If **exactly one element** is preselected when the command runs, adopt it as the anchor element.
- If nothing is preselected, or more than one element is selected, open the window with an empty state and prompt the user to press **Pick New Element**.
- A preselected element is treated as **a single element**. Do not enumerate MEP system members, do not traverse connectors. System-wide renumbering is out of scope for v1.

### 7.2 Parameter dropdown

Populated from the anchor element. Include a parameter only if **all** of these are true:

- It is an **instance** parameter (type parameters are excluded entirely in v1).
- `StorageType == StorageType.String`.
- `IsReadOnly == false`. (No formula check exists on `Parameter` in a project document — formula-driven parameters already report `IsReadOnly == true`, so this filter covers them. Do not invent a formula check.)
- `Definition` is not null (guards broken shared-parameter cases).

Include built-in, project, and shared parameters that pass. Sort alphabetically by display name, show the current value beside each name so the user can confirm they picked the right one.

If the element has zero eligible parameters, say so plainly and keep Start disabled.

### 7.3 Pattern fields

Four inputs plus a preview. The sequence mode is **auto-detected from the Seed** — there is no mode toggle:

| Field | Type | Notes |
| --- | --- | --- |
| Prefix | text | optional, free text. **Auto-prefilled with the selected parameter's current value** when the user picks a parameter, so they trim instead of typing from scratch. |
| Suffix | text | optional, free text |
| Seed | text | required. **All digits** (`01`, `100`) → numeric mode. **All letters** (`A`, `aa`) → alphabetic mode. Mixed (`A1`) is invalid — Start disabled, validation hint shown. |
| Step | integer | default 1, must be non-zero, may be negative. Applies as an integer in both modes (`A`, step 2 → `A, C, E`). |

**Numeric mode:**

- **Padding width is inferred from the Seed's length.** `01` gives 2 digit padding, `001` gives 3.
- Rollover past the padding width is allowed. After `99` comes `100`; a warning is written to the log and shown once per run in the status bar. Do not silently truncate and do not hard stop.
- With a negative step, the run **hard-stops at `00`** (status-bar message + log entry). Values below zero are never produced.

**Alphabetic mode:**

- **Excel-column progression** (bijective base-26): `X, Y, Z, AA, AB, …`
- Case follows the Seed: `a` produces `a, b, c…`; `A` produces `A, B, C…`.
- **No padding concept.** In bijective base-26 there is no zero digit, so Seed `AA` means *start at the 27th value*, not "2-wide padded A". Document this in the Seed tooltip.
- Rollover past `Z` (into `AA`) gets the same once-per-run status-bar warning as numeric `99 → 100`.
- With a negative step, the run **hard-stops at `A`/`a`**.

**Both modes:**

- Live preview label: `New value: EQ-01-A`. Updates on every keystroke.
- Reject an empty Seed; disable Start until the whole pattern is valid.
- **Auto-advance:** when a run ends for any reason (Escape, Pick New Element, document switch), the Seed field is set to the next unwritten value — numeric seeds keep their padding width (`EQ-05` ends → Seed becomes `06`) — so pressing Start again continues the sequence instead of restarting at the original seed and generating instant duplicates. **Exception:** after a negative-step hard-stop there is no valid next value, so the Seed field is left unchanged.

### 7.4 The run

A checkbox **`Restrict picking to anchor's category`** sits next to Start, **default checked**. It is read at Start and locked (disabled) while a run is active — changing selection rules mid-run would invalidate the duplicate index. It re-enables when the run ends.

Pressing **Start**:

1. Increments the run number. (**Every** Start increments it, not just Pick New Element.)
2. Configures `CategorySelectionFilter`: linked elements are **always** rejected regardless of the checkbox; the category lock to the anchor's category applies only when the checkbox is checked.
3. Opens a `TransactionGroup` named `Sequential Renumber (Run N)`.
4. **Writes the first value to the anchor element automatically** as record #1 of the run — normal `Overwrote`/`Duplicate` rules apply — and advances the counter. The first *clicked* element therefore receives value #2.
5. Enters the pick loop. For each picked element:
   - Re-resolve the element, verify it is valid and in the session document.
   - Verify the target parameter exists on it and is writable (re-resolved via the stored parameter key). If not: log `Skipped` with a reason (e.g. `parameter not present on this element` when the category restriction is off) and continue. **A skip does not advance the counter** — the sequence has no gaps.
   - Read the old value.
   - Compute the next value from the pattern.
   - Check for duplicates (7.5).
   - Open a `Transaction` named `Renumber <value>`, set the parameter, commit.
   - Append a `RenameRecord` to the report and advance the counter.
6. Escape exits the loop, the `TransactionGroup` is **assimilated** so the whole run is one undo step, the Seed field auto-advances (7.3), and control returns to the window.

If a `Transaction` fails (element owned by another user, element pinned in a way that blocks the edit, API exception), roll back **that transaction only**, log `Failed` with the reason, and keep the loop running. One bad element must not kill a 60 element run.

### 7.5 Duplicates and existing values

- Build a `DuplicateIndex` once at run start, reading the target parameter value from every element into a `HashSet<string>` (case insensitive). Scope depends on the restrict checkbox: **checked** → a `FilteredElementCollector` over the anchor's category, whole model; **unchecked** → a collector over **all non-type elements in the model** that carry the target parameter (one-time cost at Start — it is the only scope that makes "duplicate" honest when picks can come from any category). Do not run a collector on every pick.
- Update the index in memory as values are written.
- If the computed new value already exists: **write it anyway**, mark the record `Duplicate`, and render that row red in the report. No modal dialog by default.
- Add a checkbox **`Prompt me on duplicates`** (default off). When on, show a modal with Skip / Write Anyway / Stop Run.
- If the target parameter already has a non-empty value on the picked element: overwrite it, mark the record `Overwrote`, and render that row amber. The old value is preserved in the record so it can be reverted.
- **Status precedence:** a pick can be both a duplicate and an overwrite. `Duplicate` (red) wins the `Status` field; the overwrite fact goes in `Note`.

### 7.6 Report panel

A `DataGrid` in the lower half of the window, accumulating **across runs** in the session.

Columns, in order: Run, Status, Parameter, Old Value, New Value, Category, Element Id, Time, Note. The CSV export uses the same order.

Behavior:

- Selecting one or more rows highlights those elements in the model: `uidoc.Selection.SetElementIds(...)` then `uidoc.ShowElements(...)`. This is a Revit API call, so it goes through the external event.
- **Select All** button.
- **Revert Selected** button: writes `OldValue` back to each selected element inside a single transaction, marks those records `Reverted`, removes the reverted values from the duplicate index, and **re-adds the restored old values** (when non-empty) so a later run cannot unknowingly recreate a value that exists again.
- **Export CSV** button: writes the full grid to a user-chosen path via WPF's `Microsoft.Win32.SaveFileDialog` (not WinForms).
- **Clear Report** button, with a confirmation prompt. Clearing the report does **not** reset the session run counter.
- Rows for elements that no longer exist in the document must be visually flagged and excluded from highlight and revert operations.

### 7.7 Buttons

| Button | Behavior |
| --- | --- |
| **Start** | Begins the run described in 7.4: increments the run number, writes the first value to the anchor, then enters the pick loop. Disabled unless an anchor element, a target parameter, and a valid pattern all exist. |
| **Pick New Element** | Ends any active run (Seed auto-advances per 7.3), clears the anchor element and parameter selection, **keeps the report**, and prompts for a new element pick. (The run number increments on the next Start, per 7.4.) |
| **Close** | Always enabled (a mid-run click is impossible — Revit disables the window in pick mode — and a defensive guard ignores one anyway). Unregisters the external event, unsubscribes events, and closes the window. (No transaction group can be open at this point — see Section 4, rule 8.) If the report has unexported rows, ask once before closing. |

### 7.8 Edge cases

**In scope, must be handled:**

- **Worksharing:** before writing, check editability. If the element is owned by another user, log `Failed` with the owner name rather than throwing.
- **Linked models:** elements in links must not be pickable, regardless of the restrict-category checkbox. With `ObjectType.Element`, a click on a linked model resolves to the `RevitLinkInstance`, so the rejection happens in `CategorySelectionFilter.AllowElement` (reject any `RevitLinkInstance`); `AllowReference` returns false as a backstop.
- **Document switch or close mid session:** see Section 4, rule 7.
- **Element deleted between rename and report action:** see 7.6.

**Explicitly out of scope for v1. Do not implement, do not work around:**

- Type parameters.
- MEP system traversal or renumbering all members of a system.
- Elements inside groups (attempt the write, and if Revit rejects it, log `Failed`; do not add group edit handling).
- Design options handling.
- Non-string parameters (integer, double, ElementId).
- Renumbering across multiple documents or linked documents.
- Saved pattern presets or settings persistence between Revit sessions.

### 7.9 Logging

Plain text file log at:

```
%LOCALAPPDATA%\ACCO\SequentialRenumber\logs\SequentialRenumber_yyyy-MM-dd.log
```

One line per event: timestamp, level, message. Log run start and end, every rename, every skip, failure, duplicate, revert, and padding rollover. Log unhandled exceptions with full stack traces. Never let a logging failure break the tool.

---

## 8. Phases

### Phase 1 — Skeleton and external event proof

Deliverables:

- Project, `.csproj` targeting `net48`, Revit 2023 API package, correct output path and post-build copy of the DLL and `.addin` to the per-user addins folder `%AppData%\Autodesk\Revit\Addins\2023` (no admin rights needed, does not touch production ProgramData deployments).
- `SequentialRenumber.addin`, `App_SequentialRenumber` building the `Dev Tools` tab and button.
- `Cmd_SequentialRenumberTool` with the singleton guard.
- An empty modeless WPF window with the correct owner handle.
- `RenumberEventHandler` wired up, plus one trivial round trip: a temporary **Test** button that raises the external event and writes the active document title into the window's status bar.
- `FileLogger`.

**Acceptance:** Revit 2023 loads the add-in with no errors, the button appears on `Dev Tools`, the window opens modeless, stays on top of Revit, does not open twice, and the Test button proves the ExternalEvent round trip works. Stop here and report.

### Phase 2 — Selection, parameters, pattern, and the run

Deliverables: sections 7.1 through 7.4, plus `ISequenceFormatter` with `NumericSequenceFormatter` and `AlphaSequenceFormatter`, `ParameterScanner`, `CategorySelectionFilter`, the `Restrict picking to anchor's category` checkbox, `IPickStrategy` and `PickObjectLoopStrategy`, transaction handling, and the in-memory list of `RenameRecord`. (The duplicate index and its whole-model scope when the checkbox is unchecked are Phase 3 — in Phase 2 duplicates are simply not checked.)

**Acceptance:** with a piece of equipment preselected, the dropdown lists only writable string instance parameters with their current values. Setting `EQ-`, `01`, step `1`, suffix blank and pressing Start writes `EQ-01` to the anchor immediately, then clicking nine elements produces `EQ-02` through `EQ-10`. Seed `A` produces `EQ-A`, `EQ-B`, … the same way. Escape ends the run cleanly and the Seed field reads `11` (or the next letter). A single Ctrl+Z reverts the whole run. Stop here and report.

### Phase 3 — Report, duplicates, revert

Deliverables: sections 7.5, 7.6, 7.7, 7.8.

**Acceptance:** duplicates flag red without a modal, the prompt checkbox works, row selection highlights and zooms the elements in the model, Revert Selected restores the old values in one undo step, CSV export opens correctly in Excel, and killing an element then clicking its row does not throw.

---

## 9. Definition of done

- Builds clean with no warnings that were not already there.
- No Revit API call outside `Cmd_SequentialRenumberTool.Execute` (startup snapshot) and `RenumberEventHandler.Execute` (see Section 4, rule 1).
- No stored `Element` references anywhere in the codebase.
- Everything in `Core\` compiles without a reference to the Revit API. (No unit test project in v1; `Core\` stays testable for later.)
- No API used that is unavailable in Revit 2023.
- XML doc comments on every public type and method explaining intent, not restating the signature.

---

## 10. Decision log

**2026-09-02** — spec review with Claude Code before Phase 1:

| # | Decision |
| --- | --- |
| A | Start writes the first value to the anchor element automatically (record #1 of the run). |
| B | Seed auto-advances to the next unwritten value when a run ends; every Start increments the run number. |
| C | Status precedence: `Duplicate` beats `Overwrote`; the other fact goes in `Note`. |
| D | Negative step hard-stops the run at `00` (numeric) / `A` (alphabetic). |
| E | Letter sequences: mode auto-detected from the Seed (digits vs. letters), Excel-style bijective base-26, case follows seed, no padding in letter mode. Mixed seeds invalid. |
| F | `Restrict picking to anchor's category` checkbox, default checked; unchecked widens the duplicate index to the whole model; links never pickable either way. |
| — | `RenameRecord.ElementIdValue` is `long` (2024+ port); owner handle via `UIApplication.MainWindowHandle`; API access allowed in the command's Execute for the startup snapshot; no formula check on parameters (`IsReadOnly` covers it); link rejection in `AllowElement`; revert re-adds restored values to the duplicate index; dev deploy to `%AppData%\Autodesk\Revit\Addins\2023`; CSV via WPF `SaveFileDialog`. |

**2026-09-02** — Phase 2 UI review:

| # | Decision |
| --- | --- |
| G | Selecting a parameter auto-prefills Prefix with that parameter's current value (user trims rather than retypes). |
| H | Step field is labeled **"Increment by"** in the UI (the spec keeps "Step" as the concept name). Close button is labeled **"Done"**. All four pattern labels carry hover tooltips. Dev ribbon tab is named **"ORH Dev"** (user preference over the spec's original "Dev Tools"). |
| I | **Done** is enabled only after the first run has started (the window's X still closes at any time). Window-close cleanup (event unsubscription + external event disposal) is routed through a final Cleanup external-event raise — see Section 4, rule 8. |
| J | Preview label reads **"New value:"** (was "Next value:"). The close button's gate is `HasRunStarted` alone — Revit disables the whole owned window during pick mode (`EnableWindow`), so mid-run graying is Revit's, not ours, and a mid-run click is impossible (a defensive guard ignores one anyway). Esc is the only mid-run stop. |
| K | Close button renamed back to **"Close"** (since Revit's pick-mode disable makes a mid-run "Done" click impossible, the Done framing added nothing). The Start button is wider and its label changes to **"Esc to stop"** while a run is active. |
| L | Close is **always enabled** — the enabled-after-first-Start gate (decision I) is retired: it made sense for a "Done" button but a disabled Close button just reads as broken. The mid-run defensive guard stays. |
