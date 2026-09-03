# SequentialRenumber

A modeless Revit add-in that sequentially renumbers a **text (string) instance parameter** across elements you click in order — replacing the tedium of typing `EQ-01`, `EQ-02`, `EQ-03` (or `VAV 1-A`, `VAV 1-B`, …) into the Properties palette one element at a time.

Pick a starting element, choose which parameter to write, define a Prefix / Seed / Increment / Suffix pattern, press **Start**, then click elements in the model one after another. Every click writes the next value in the sequence and logs it to a report you can review, highlight from, revert, and export.

> **Status:** v1 feature-complete. Dev sandbox targeting **Revit 2023** only; built to port cleanly into the multi-year `BTT_ACCORevit-Ribbons` solution (Revit 2023–2027). The authoritative specification, including a decision log of every design choice, lives in [`SequentialRenumber.md`](SequentialRenumber.md).

---

## Table of contents

- [Using the tool](#using-the-tool)
- [The pattern](#the-pattern)
- [Options](#options)
- [The report](#the-report)
- [Undo model](#undo-model)
- [Edge cases handled](#edge-cases-handled)
- [Known limitations](#known-limitations)
- [How it works (architecture)](#how-it-works-architecture)
- [Project layout](#project-layout)
- [Building and deploying](#building-and-deploying)
- [Logging](#logging)
- [Porting notes (Revit 2024+)](#porting-notes-revit-2024)

---

## Using the tool

1. **(Optional) preselect one element**, then launch **Sequential Renumber** from the dev ribbon tab. A single preselected element is adopted as the *anchor*; otherwise press **Pick New Element** and click one in the model.
2. **Choose the parameter to write.** The dropdown lists only parameters that are safe to write: *instance* parameters, *text* (string) storage, *not read-only*. Each entry shows the anchor's current value so you can confirm you picked the right one.
3. **Define the pattern.** Selecting a parameter auto-fills **Prefix** with its current value — trim it to the part you want to keep instead of typing from scratch. Set the **Seed**, **Increment by**, and optional **Suffix**. The **New value:** preview updates on every keystroke; Start stays disabled until the pattern is valid.
4. **Press Start.** The anchor element receives the first value immediately, then Revit enters pick mode: every element you click gets the next value in the sequence. The button reads **Esc to stop** while running.
5. **Press Esc** to end the run. The Seed auto-advances to the next unused value, so pressing Start again continues the sequence instead of restarting it.
6. Review the **report**, revert anything that went wrong, export to CSV, and **Close**.

To renumber a different set of elements (or a different parameter), press **Pick New Element** — the report is kept across runs.

## The pattern

Four fields build each value: `Prefix + counter + Suffix`. The counter mode is **auto-detected from the Seed** — there is no mode toggle:

| Seed | Mode | Produces |
| --- | --- | --- |
| `01` | Numeric, 2-digit padding | `01, 02, … 99, 100` |
| `001` | Numeric, 3-digit padding | `001, 002, …` |
| `A` | Alphabetic (Excel-style) | `A, B, … Z, AA, AB, …` |
| `aa` | Alphabetic, lowercase | `aa, ab, …` |
| `A1` | ❌ invalid | Mixed seeds are rejected; Start stays disabled |

**Numeric mode**

- Padding width is inferred from the Seed's length (`01` → 2 digits, `001` → 3).
- Rolling past the padding is allowed (`99` → `100`) — a one-time note appears in the status bar and the log. Nothing is truncated and the run does not stop.
- A negative increment hard-stops the run at `00`; values below zero are never produced.

**Alphabetic mode**

- Excel-column progression (bijective base-26): `X, Y, Z, AA, AB, …`
- Output case follows the Seed (`a` → `a, b, c…`; `A` → `A, B, C…`).
- There is **no padding concept** — base-26 has no zero digit, so a Seed of `AA` means *start at the 27th value*, not "2-wide padded A".
- A negative increment hard-stops at `A`/`a`.

**Both modes**

- **Increment by** is a whole number, non-zero, may be negative (`A` with increment 2 → `A, C, E`).
- **Seed auto-advance:** when a run ends, the Seed field is set to the next unwritten value (keeping its padding), so re-Starting continues the sequence rather than creating instant duplicates. After a negative-increment hard stop the Seed is left unchanged.

## Options

| Option | Default | Effect |
| --- | --- | --- |
| **Restrict picking to anchor's category** | ✅ on | Only elements of the anchor's category are clickable during the run. When off, any model element is pickable — elements that don't carry the target parameter are skipped *without consuming a number*. Elements inside linked models are never pickable either way. Read at Start and locked while a run is active. |
| **Prompt me on duplicates** | ⬜ off | When a computed value already exists in the model, ask **Skip / Write Anyway / Stop Run** instead of writing it silently (Esc in the dialog counts as Skip). |

## The report

A grid in the lower half of the window accumulates one row per action, across every run in the session.

**Columns:** Run, Status, Parameter, Old Value, New Value, Category, Element Id, Time, Note *(the CSV export uses the same order)*.

**Statuses and row colors:**

| Status | Color | Meaning |
| --- | --- | --- |
| `Applied` | — | Value written to a previously empty parameter. |
| `Duplicate` | 🟥 red | Value was written but already exists elsewhere in the model. If it also overwrote a value, that fact is in Note — Duplicate outranks Overwrote. |
| `Overwrote` | 🟨 amber | Parameter had a non-empty value; it was replaced (old value preserved in the row). |
| `Skipped` | — | Nothing written (parameter missing on the element, or duplicate skipped via the prompt). Skips never consume a sequence number. |
| `Failed` | red text | The write failed (element owned by another user, API error). The reason is in Note; the run keeps going. |
| `Reverted` | gray | Old value restored by **Revert Selected**. |

Rows whose element no longer exists in the document are flagged gray and excluded from highlight and revert — clicking them never throws.

**Actions:**

- **Row selection** highlights and zooms to those elements in the model (single or multi-select).
- **Select All** selects every row.
- **Revert Selected** writes each row's Old Value back in a **single transaction** — one Ctrl+Z undoes the whole revert. Only `Applied` / `Duplicate` / `Overwrote` rows are revertible; the duplicate index is kept truthful (reverted values removed, restored values re-added).
- **Export CSV** writes the grid to a path you choose (UTF-8 with BOM, proper quoting — opens cleanly in Excel).
- **Clear Report** empties the grid after confirmation. It does not undo any renames, and the run counter keeps counting.

Closing the window with unexported rows asks once for confirmation.

## Undo model

- Each element write is its own transaction, so **one bad element never kills the run** — it logs `Failed` and the loop continues.
- All of a run's transactions live inside a `TransactionGroup` that is assimilated when the run ends, so **a single Ctrl+Z undoes the entire run**.
- **Revert Selected** is a single transaction — one undo step of its own.

## Edge cases handled

- **Worksharing:** before every write the element's checkout status is checked; an element owned by another user logs `Failed — Owned by <name>` instead of throwing.
- **Linked models:** never pickable, regardless of the category checkbox.
- **Document switched:** if a different document becomes active, Start disables with a status message; switching back re-enables it. The tool never writes to the wrong document.
- **Document closed:** the session ends cleanly; pick a new element to start over.
- **Anchor or report element deleted:** detected on next use, flagged, excluded — no exceptions.
- **Elements in groups:** the write is attempted; if Revit rejects it, the row logs `Failed`.

## Known limitations

- **While a run is active the window is not clickable** — Revit disables owned windows in pick mode. Press **Esc** to end the run; the Start button reads *Esc to stop* as a reminder.
- v1 is deliberately scoped out of: type parameters, non-string parameters, MEP system traversal, design options, renumbering in linked documents, and saved pattern presets (see spec §7.8).

## How it works (architecture)

The add-in follows the production ACCO modeless pattern: **singleton WPF window + `ExternalEvent` / `IExternalEventHandler`**.

```
┌────────────────────┐  sets Request, Raise()   ┌──────────────────────────┐
│ SequentialRenumber │ ───────────────────────▶ │ RenumberEventHandler      │
│ Window (WPF,       │                          │ .Execute(app)             │
│ modeless, no API)  │ ◀─────────────────────── │ the ONLY API entry point  │
└────────────────────┘   updates ViewModel      │ besides the command's     │
                                                │ startup snapshot          │
                                                └──────────────────────────┘
```

- **Two API contexts only.** `Cmd_SequentialRenumberTool.Execute` takes the startup snapshot (preselection + parameter scan); everything else — picking, writing, highlighting, reverting, even event unsubscription — runs inside `RenumberEventHandler.Execute`, which Revit guarantees is a valid API context.
- **The run is a pick loop inside one `Execute` call.** `PickObject` is legal there; Escape surfaces as `Autodesk.Revit.Exceptions.OperationCanceledException` and ends the loop. `IPickStrategy` abstracts "get me the next element" so a `SelectionChanged`-based strategy can drop in for newer Revit versions without touching the engine.
- **No stored `Element` references — ever.** The anchor and every report row hold a `long` element id plus document identity, re-resolved via `doc.GetElement` and checked with `IsValidObject` on every use.
- **Parameter identity survives across elements.** Matching by display name is fragile, so the chosen parameter is keyed by `BuiltInParameter` id (built-ins), shared-parameter GUID (shared), or definition name (project parameters, the documented fallback) and re-resolved per pick. Each report row carries its key so revert targets the exact parameter even after later runs switch parameters.
- **Duplicates are detected against an in-memory index** built by *one* `FilteredElementCollector` at run start (anchor category when restricted, whole model when not) — never a collector per pick. Writes, reverts, and restores keep it current. An element's own current value is not counted as a duplicate of itself.
- **Window-close cleanup goes through the API context.** Unsubscribing Revit application events (`ViewActivated`, `DocumentClosing`) outside an API context throws — so closing the window raises one final `Cleanup` request, and the handler unsubscribes and disposes the external event from inside `Execute`.
- **`Core\` has zero Revit references.** Sequence math (numeric padding, Excel-style bijective base-26 letters), session state, records, the duplicate index, and CSV building are plain .NET — unit-testable by compiling them against any framework, no Revit install required.

## Project layout

```
SequentialRenumber\
  Cmd_SequentialRenumberTool.cs      IExternalCommand entry point, singleton guard, startup snapshot
  App_SequentialRenumber.cs          IExternalApplication, builds the dev ribbon tab
  SequentialRenumber.addin           Manifest (registers the application)

  UI\                                WPF layer — no Revit API calls
    SequentialRenumberWindow.xaml    Modeless window (anchor, parameter, pattern, report)
    RenumberViewModel.cs             Binding surface, INotifyPropertyChanged
    ReportRow.cs                     Grid wrapper around one RenameRecord

  Revit\                             Everything that touches the Revit API
    RenumberEventHandler.cs          IExternalEventHandler — the run engine
    IPickStrategy.cs                 "Get me the next element" abstraction
    PickObjectLoopStrategy.cs        Revit 2023 implementation (PickObject loop)
    CategorySelectionFilter.cs       Rejects links always; optional category lock
    ParameterScanner.cs              Discovers eligible parameters; re-resolves by key
    ElementHighlighter.cs            SetElementIds + ShowElements for report rows
    RevitVersionCompat.cs            ElementId <-> long, isolated for the 2024+ port

  Core\                              No Revit API references — portable, testable
    ISequenceFormatter.cs            Next-value contract + mode-detecting factory
    NumericSequenceFormatter.cs      Digit seeds: padding, rollover
    AlphaSequenceFormatter.cs        Letter seeds: Excel-style bijective base-26
    RenumberSession.cs               Run state: pattern, counter, rollover flag
    RenameRecord.cs                  One logged rename
    DuplicateIndex.cs                Case-insensitive set of existing values
    CsvExporter.cs                   Manual CSV building with escaping

  Infrastructure\
    FileLogger.cs                    Daily plain-text log; never throws
```

## Building and deploying

**Requirements:** Visual Studio 2022+ (or the .NET SDK; .NET 8 SDK for the 2025/2026 configs), Revit 2023–2026.

```bash
dotnet build "SequentialRenumber/SequentialRenumber.csproj" -c "Debug R23"
```

- Configurations: `Debug R23`–`Debug R26` and `Release R23`–`Release R26`. R23/R24 target `net48`; R25/R26 target `net8.0-windows`. Each configuration defines `REVIT<year>`, which drives the single `#if` guard in `RevitVersionCompat.cs`.
- The Revit API comes from the `Revit_All_Main_Versions_API_x64` NuGet package — no local Revit path needed to compile.
- A post-build step copies the `.addin` manifest and DLL to the per-user addins folder for the built year (`%AppData%\Autodesk\Revit\Addins\<year>`), so the next Revit start loads the new build. No admin rights required.
- Debugging: the project is configured to launch `Revit.exe` directly (F5 from Visual Studio).

The button appears on the dev ribbon tab (`ORH Dev` → *In Development* → **Sequential Renumber**).

## Logging

A plain-text daily log records run starts/ends, every rename, skip, failure, duplicate, revert, rollover, and any unhandled exception with a full stack trace:

```
%LOCALAPPDATA%\ACCO\SequentialRenumber\logs\SequentialRenumber_yyyy-MM-dd.log
```

A logging failure never breaks the tool.

## Porting notes (Revit 2024+)

The sandbox was built so the move into `BTT_ACCORevit-Ribbons` (Revit 2023–2027 across `net48` / `net8.0-windows` / `net10.0-windows`) is close to copy-and-paste:

- **`RevitVersionCompat.cs` is the single `#if` point** for the `ElementId.IntegerValue` (int, ≤2023) → `ElementId.Value` (long, 2024+) change — the guards are already in place and every id flows through it as a `long`.
- `Core\` moves untouched — it has no Revit references.
- `IPickStrategy` lets a `SelectionChanged`-based picker replace the `PickObject` loop behind a version guard without touching the run engine.
- No WinForms, no third-party packages, no API newer than Revit 2023.

See [`SequentialRenumber.md`](SequentialRenumber.md) — sections 3 (port constraints) and 10 (decision log) — for the full history of what was decided and why.
