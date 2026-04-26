# BinlogMcp guide for LLMs

A concise field manual for navigating an MSBuild `.binlog` through this MCP.
Pair this with `get_search_syntax_help` (full DSL reference).

## What you're looking at

A binlog is the recorded execution tree of one MSBuild build. Major node kinds:

- `Build` (root) → `Project` (one per project execution, can repeat per target/TFM)
- `Project` → `Target` → `Task` → `Message` / `Error` / `Warning`
- `ProjectEvaluation` (separate from execution) holds evaluated `Property` and `Item` folders
- `Folder` groups things (`Properties`, `Items`, `Imports`, `Errors`, `Warnings`, ...)
- `Import` / `NoImport` show every `<Import>` resolved during evaluation
- `AddItem` / `RemoveItem` log item-group mutations during target execution
- `CopyTask` produces virtual file-copy results surfaced through `$copy`

The same `Project` can appear many times — once per target framework, target list, or `MSBuild` task call.

## Ids

Every addressable node has an id printed as `[123]` or `[42/3.7]`.

- `42` → the `TimedNode` whose `Index == 42`
- `42/3.7` → child 7 of child 3 of node 42

Ids are stable for the same binlog file bytes (so survive `reload_binlog` of an unchanged file). They are **not** portable across different binlogs and become invalid once the file is overwritten by a new build.

On `Project` lines the ` → <name>` shows the entry target; the `[id]` is still the `Project` node, not the target.

## The core loop

1. **`search <query>`** — locate matches. Returns a tree grouped by Project / Target / Task.
2. **Pick an id** from the output.
3. **Explore from there**:
   - `get_node <id>` — kind, summary, parent, child count, source location, timing
   - `get_children <id> [kind=…] [nameContains=…]` — drill into children, filter to avoid 10 000 properties
   - `get_ancestors <id>` — full path back to root ("where did this happen?")
   - `print_subtree <id> [maxDepth] [maxNodes]` — viewer-style indented dump
4. **Re-search** with refined `under(...)` clauses anchored at what you found.

Avoid raw paging through huge child lists. Filter with `kind` (any `$kind` token without the `$`) and/or `nameContains`.

## DSL essentials

| Token | Meaning |
|-------|---------|
| `Csc` | substring (case-insensitive) anywhere |
| `"Copying file"` | literal phrase |
| `$error` `$warning` `$message` | by node kind |
| `$task` `$target` `$project` `$projectevaluation` | by container kind |
| `$task Csc` | tasks named Csc; `$csc` and `$rar` are aliases |
| `$property name=Foo value=Bar` | precise field match |
| `$42` | node with `Index == 42` |
| `under($project Foo) X` | X anywhere under a project named Foo |
| `notunder($task Csc) error` | exclude a subtree |
| `X project(Foo.csproj)` | nearest-parent Project filter |
| `not($task Csc)` | exclude term |
| `$task $time` | append durations, sort slowest first |
| `$project $start` / `$target $end` | sort by start/end time |
| `$copy [name]` | file copies via `FileCopyMap` |
| `$nuget project(Foo.csproj)` | NuGet package graph for one project |
| `$projectreference project(Foo.csproj)` | project reference graph |

Compose freely: `$additem under($target CoreCompile project(Foo.csproj))`.

## Counts vs samples

`search` is capped (default 200, max 5000). The header may end with `matched=N+` meaning the cap was hit. To get the real total without paginating, use **`count <query>`** — same DSL, no result formatting, no cap.

Decision rule:
- "show me a few" → `search`
- "is it 5 or 5 000?" → `count`

## Source text vs node text

Two different worlds:

- **Node text** (`get_node`, `print_subtree`, search results) is what MSBuild logged for a node. Truncated to ~300 chars in summaries; `get_node` returns the full untruncated text.
- **Source text** is the actual file contents the binlog *embeds* (project files, props, targets, response files):
  - `list_files` — what's available
  - `read_file <fullPath> [startLine] [endLine]` — read embedded text (1-based, inclusive)
  - `search_files <pattern>` — full-text grep across embedded files
- **`preprocess <evaluationId>`** flattens all `<Import>`s into one virtual file, identical to the viewer's "Preprocess" — the only way to see the fully-expanded targets/props for an evaluation. Cached after first call. Supports `startLine`/`endLine`.

When the binlog doesn't record what you need (e.g. evaluated item provenance), fall back to source text search.

## Recipes

### Cold-start triage (do this first on any unfamiliar binlog)
1. `load_binlog <path>` — note `succeeded`, `duration`, `msbuildVersion`.
2. `count $error` and `count $warning` — decide if this is a failure investigation or a perf one.
3. If failed: `search $error` (default 200 is enough for almost any build); for each unfamiliar one `get_ancestors <id>` to see *which project / target / task* produced it.
4. If succeeded but slow: `search $task $time maxResults=20` and `search $target $time maxResults=20` to find hotspots, then drill in.
5. `search $project` (or `count $project`) to gauge build shape — many repeats per project usually means multi-targeting or `MSBuild` task fan-out, not a problem in itself.

### Why was `Foo.dll` copied to OutputDir?
1. `search $copy Foo.dll` → see candidate copy results.
2. Pick the full source path from the result and `search $copy <full-path>`. The expanded result tree shows the project / target / task path responsible (Build outputs, RAR `Resolved file path`, `_CopyFilesMarkedCopyLocal`, NuGet content files, `None Include="…" CopyToOutputDirectory=…`, project reference outputs, …).
3. If still ambiguous: `get_ancestors <id>` on a copy result gives full provenance.

`TryExplainSingleFileCopy` (the engine behind `$copy <fullpath>`) folds in incoming/outgoing copies, RAR resolved paths, NuGet content, project reference outputs and copy-to-publish flows — a single `$copy <fullpath>` query usually answers "why".

### Where was item `@(X)` added?
Items added at evaluation time are not individually attributed in the binlog — only the final list survives. Strategy:

1. `search_files "<X Include="` to find every static `ItemGroup` that could contribute. Reason about conditions.
2. For items added during execution, those *are* logged: `search $additem` (optionally `under($target Foo project(Bar.csproj))`).
3. To inspect the final value: `search $projectevaluation Bar`, take its id, then `search_properties_and_items <id> $item X`.

### Why is property `P` set to this value?
1. `search $projectevaluation Foo` → take evaluation id.
2. `search_properties_and_items <id> $property name=P` — returns the property and (when only properties are matched) a "Property Graph" folder showing reassignments.
3. For execution-time changes: `search $property name=P under(project(Foo.csproj))` for assignments inside targets.

### Why did target `T` run / skip?
1. `search $target T project(Foo.csproj)`.
2. `get_node <id>` → check `Skipped` / start / end / duration.
3. `get_ancestors <id>` to see who invoked it.
4. `get_children <id> kind=message` for "Target X was skipped because…" messages.

### Slowest tasks / longest targets
- `search $task $time` — tasks sorted by duration descending, with a `Total duration` header.
- `search $target $time project(Foo.csproj)` — same, scoped to one project.
- Combine with `under(...)` to focus a slow phase.

### All errors with surrounding context
1. `count $error` — quick triage.
2. `search $error` (default 200 is plenty for most builds) → grouped by project/target/task automatically.
3. `get_ancestors <id>` on each unfamiliar one.

### Inspect a Csc command line
1. `search $csc project(Foo.csproj)` → pick the Csc task id.
2. `print_subtree <id> maxDepth=3` shows `Parameters` and `CommandLineArguments` folders (often huge — `get_node` on the `CommandLineArguments` child returns the full string).
3. To see what was compiled: `get_children <id> nameContains=Sources` then drill into the matching parameter folder.

### Why is the *incremental* build doing work? (the up-to-date check)
Goal: find targets that re-ran on a no-change rebuild and shouldn't have.

1. Produce two binlogs:
   - Clean build: `dotnet build /bl:1.binlog` (or `msbuild /bl:1.binlog`).
   - **Immediate** repeat with no source edits: `dotnet build /bl:2.binlog`. The second is the interesting one — it represents the steady-state incremental cost.
2. `load_binlog 2.binlog` then `search "Building target" "completely"`. MSBuild logs `Building target "X" completely.` whenever it runs a target whose up-to-date check failed; the very next message under the target usually explains *why* (e.g. `Input file ".../Foo.cs" is newer than output file ".../Foo.dll"`, or `Output file ".../X" does not exist`).
3. For each hit, get the surrounding context:
   - `get_ancestors <id>` to see project / TFM.
   - `get_node <parentTargetId>` for full timing — use this to gauge how *expensive* the rerun was.
4. **Filter the noise.** Many targets have no `Inputs`/`Outputs` declared and are always considered out-of-date by design (`GenerateTargetFrameworkMonikerAttribute`, `GetCopyToOutputDirectoryItems`, `_CheckForInvalidConfigurationAndPlatform`, `GetTargetFrameworks*`, most `_*` housekeeping targets, anything in `Restore`). They show up but they're cheap and expected.
5. **Focus on heavy targets** where reruns actually cost: `CoreCompile` (Csc), `ResolveAssemblyReferences` (RAR), `_CopyOutOfDateSourceItemsToOutputDirectory`, `GenerateDepsFile`, `GenerateRuntimeConfigurationFiles`, `_CopyFilesMarkedCopyLocal`, anything Roslyn-source-generator-related. A useful filter:
   - `search "Building target" "completely" under($target CoreCompile)` — Csc reruns specifically.
   - Cross-check with `search $task $time maxResults=20` — if `Csc` shows up high here on the second binlog, you've confirmed real incremental cost.
6. Common root causes you'll see in the "why" message:
   - An *output* path that points outside the project's `obj/`/`bin/` (so it never exists on the first run after clean of just *this* project).
   - A timestamp inversion from a `Touch` / generated-file step earlier in the same build.
   - `Inputs` glob picking up files that change every build (e.g. logs, generated `.g.cs` with timestamps).
   - Missing `Outputs` declaration (the target opted out of incrementality entirely — fix the target).

### Compare two builds (fast vs slow, succeeded vs failed)
Load both with `load_binlog`, then run the *same* query against each path. Useful staples:

- `count $error` and `count $warning` per file
- `search $task $time` with `maxResults=20` per file
- `search $target Build $time` per file
- `count $project` per file (parallelism / repeat-execution check)

Ids are not portable between the two — re-issue searches per binlog.

### Find an MSBuild task call chain
`MSBuild` task calls cross project boundaries. From a deep `Project` node, `get_ancestors` walks up through the `MSBuild` tasks back to the entry project. `notunder($task MSBuild)` is sometimes useful to filter out re-entrant noise.

## Pitfalls

- **`matched=N+`** means the cap was hit — use `count` if you need the truth.
- **`Project` repeats** are normal (per TFM / per target call). Distinguish with the ` → <entry target>` suffix and `EvaluationText`.
- **`get_children` on a `Project`** can return tens of thousands of `Property` children. Always pass `kind=` or `nameContains=` first.
- **Synthetic nodes** (totals, NuGet/`$copy` groupings, "Property Graph", "Imports") have no id and can't be navigated to with `get_node`. They're presentation only.
- **Source text is what the binlog embedded**, not the current state of disk. `read_file` reads embedded snapshots.
- **Evaluation ≠ Execution.** Property/Item folders under `ProjectEvaluation` are post-evaluation snapshots; mutations in targets are separate `AddItem` / `Property` log entries under `Project` nodes.
- **`reload_binlog` against a changed file** invalidates ids you already returned; discard them.

## Quick reference

```
load_binlog       reload_binlog       list_loaded_binlogs       unload_binlog       unload_all_binlogs
search            count               get_search_syntax_help
get_node          get_children        get_ancestors             print_subtree
search_properties_and_items
list_files        read_file           search_files              preprocess
```
