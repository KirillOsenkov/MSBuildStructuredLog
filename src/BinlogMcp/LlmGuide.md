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

Node ids print as `[123]` (the `TimedNode` with `Index == 123`) or `[42/3.7]` (child 7 of child 3 of node 42). Stable for the same binlog file bytes; invalid once the file is overwritten by a new build.

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

Use `preprocess_file` when the question is about the effective MSBuild XML for one evaluation after imports and conditions: whether a target/property/item definition was actually imported, which assignment appears later and wins, what `DependsOnTargets` / `BeforeTargets` / `AfterTargets` says in this evaluation, or whether a target is absent because its declaring file was not imported. Prefer raw `search_files` when you are looking for text anywhere in embedded source files regardless of evaluation context.

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

## Recipes

### Cold-start triage (do this first on any unfamiliar binlog)
1. `load_binlog <path>` — note `succeeded`, `duration`, `msbuildVersion`.
2. `count $error` and `count $warning` — decide if this is a failure investigation or a perf one.
3. If failed: `search $error` (default 200 is enough for almost any build); for each unfamiliar one `get_ancestors <id>` to see *which project / target / task* produced it.
4. If succeeded but slow: `search $task $time maxResults=20` and `search $target $time maxResults=20` to find hotspots, then drill in.
5. `search $project` (or `count $project`) to gauge build shape — many repeats per project usually means multi-targeting or `MSBuild` task fan-out, not a problem in itself.

### Which .NET SDK built this? (do this very early — often the answer)
**Knowing the SDK version is critical.** The same source frequently builds clean on one SDK and fails on another (new analyzers, retargeted defaults, changed `Sdk.props`/`Sdk.targets`, NuGet resolver changes). Whenever a build "started failing" or behaves differently between two machines / CI legs, pin down the SDK version *before* anything else.

1. `search_files Sdk.props` — the embedded files archive contains every file MSBuild read during the build. The SDK version is encoded **in the path itself**: e.g. `C:\Program Files\dotnet\sdk\10.0.202\Sdks\Microsoft.NET.Sdk\Sdk\Sdk.props` → SDK `10.0.202`. On Linux/macOS the prefix is `/usr/share/dotnet/sdk/<version>/...` or `/usr/local/share/dotnet/sdk/<version>/...`.
2. Multiple SDK paths in the results → multiple SDKs were resolved (e.g. `Microsoft.NET.Sdk`, `Microsoft.NET.Sdk.Web`, `Microsoft.Build.NoTargets`). All ship inside the same `dotnet/sdk/<version>` folder, so the version segment should match across them — a mismatch points at a `global.json` / SDK-resolver oddity.
3. Cross-check with `msbuildVersion` from `load_binlog` — the .NET SDK bundles a specific MSBuild build, so the two should be consistent.
4. To compare two binlogs: run the same `search_files Sdk.props` on each; differing version segments alone often explain "works here, breaks there" without any further digging.

### Why was `Foo.dll` copied to OutputDir?
1. `search $copy Foo.dll` → see candidate copy results.
2. Pick the full source path from the result and `search $copy <full-path>`. The expanded result tree shows the project / target / task path responsible (Build outputs, RAR `Resolved file path`, `_CopyFilesMarkedCopyLocal`, NuGet content files, `None Include="…" CopyToOutputDirectory=…`, project reference outputs, …).
3. If still ambiguous: `get_ancestors <id>` on a copy result gives full provenance.

`TryExplainSingleFileCopy` (the engine behind `$copy <fullpath>`) folds in incoming/outgoing copies, RAR resolved paths, NuGet content, project reference outputs and copy-to-publish flows — a single `$copy <fullpath>` query usually answers "why".

### Where was item `@(X)` added?
Items added at evaluation time are not individually attributed in the binlog — only the final list survives. Strategy:

1. `search_files "<X Include="` where `X` is the item name (for example `search_files "<Compile Include="` or `search_files "<None Include="`). This is the fastest way to find static `ItemGroup` entries that add items during evaluation. Reason about surrounding `Condition`s and imports.
2. For items added during execution, those *are* logged: `search $additem` (optionally `under($target Foo project(Bar.csproj))`).
3. To inspect the final value: `search $projectevaluation Bar`, take its id, then `search_properties_and_items <id> $item X`.

### Why is property `P` set to this value?
1. `search $projectevaluation Foo` → take evaluation id.
2. `search_properties_and_items <id> $property name=P` — returns the property and (when only properties are matched) a "Property Graph" folder showing reassignments.
3. If multiple imported files could set the property, use `preprocess_file` to inspect the actual import order and nearby conditions for this evaluation.
4. For execution-time changes: `search $property name=P under(project(Foo.csproj))` for assignments inside targets.

If the question depends on the full *lifetime* of a property during evaluation and the current binlog only shows the final value, recommend that the user rebuild with `MSBUILDLOGPROPERTYTRACKING=15` set in the environment before invoking MSBuild. That enables additional build messages for every property assignment and reassignment during evaluation; without it, you usually only see the final evaluated property value plus whatever reassignment information the normal property graph can infer.

### Why did target `T` run / skip?
1. `search $target T project(Foo.csproj)`.
2. `get_node <id>` → check `Skipped` / start / end / duration.
3. `get_ancestors <id>` to see who invoked it.
4. `get_children <id> kind=message` for "Target X was skipped because…" messages.

### Where (and how) was `Foo.targets` imported?
1. `search $import Foo.targets project(Bar.csproj)` — every successful import of that file in Bar's evaluation. The result tree shows the importing file/line and the resolved target.
2. If nothing comes back, try `search $noimport Foo.targets project(Bar.csproj)` — `NoImport` nodes capture imports that *were attempted but skipped*. The reason is in the node text: false `Condition`, file not found at the resolved path, MSBuild SDK not resolved, etc.
3. Drop the `project(...)` clause to see imports across all evaluations (useful when you don't yet know which project pulled it in, or when chasing SDK-level imports).
4. When import search shows that a file was imported but you need to understand what it contributed, use `preprocess_file` on the project evaluation and search/read the preprocessed XML.

### Read the preprocessed XML for an evaluation
`preprocess_file` returns the full effective MSBuild XML for a project (all `<Import>`s recursively inlined — same as `msbuild /pp` and the viewer's "Preprocess" command). Scoped to one `ProjectEvaluation`.

1. `search $projectevaluation Foo` → take the evaluation id.
2. `preprocess_file <binlog> <evalId>` → defaults to the project file itself; pass `file=<path-or-suffix>` to preprocess a specific imported `.props`/`.targets` instead. Pages 500 lines at a time (`startLine`/`endLine`).

What it shows you, all from one place:
- The exact order of `<PropertyGroup>` elements (so you can see the *last* assignment that wins) and `<ItemGroup>` elements (so you can see what the item list looked like at end of evaluation).
- Which targets are actually in scope for this evaluation (anything not in the preprocessed text was filtered out by an unmet `Condition` or simply not imported).
- Which `.props`/`.targets` files were imported (each inlined block is preceded by a comment with the source path) and which were skipped.

### Why did target `T` not run in project `Foo`?
The execution log only tells you what *did* run. Combine preprocess + import searches to figure out what *should have* run and where the chain broke.

1. **Did the target even reach this evaluation?** `search $projectevaluation Foo` → take id, `preprocess_file <binlog> <evalId>` → grep (e.g. `search_files "<Target Name=\"T\""`) the preprocessed text. If `T` is absent, it was excluded by a `Condition` somewhere upstream, or its declaring file wasn't imported.
2. **If absent, find where it's declared and check the import chain.** Identify the file declaring `T` (often a `.targets` from an SDK or NuGet package). Then `search $noimport <file>.targets project(Foo.csproj)` — a hit gives you the reason (false Condition, missing file, etc.). If `$noimport` finds nothing either, walk further up: `search $import <parent>.targets project(Foo.csproj)` to see whether the parent that should pull it in even loaded.
3. **If present in the preprocessed text but still didn't run**, it was in scope but never invoked. Walk these in order:
   - **Own `Condition`**: read the `<Target ... Condition="...">` line in the preprocessed XML; check the values of the referenced properties via `search_properties_and_items <evalId> $property name=<P>`.
   - **Listed in another target's `DependsOnTargets`**: `search_files "DependsOnTargets" T` (or grep the preprocessed text). Then `search $target <Caller> project(Foo.csproj)` to see whether the caller actually ran (it may itself be skipped).
   - **Hooked via `BeforeTargets` / `AfterTargets`**: read the target's `BeforeTargets="..."`/`AfterTargets="..."` attributes from the preprocessed XML, then verify each anchor target ran: `search $target <Anchor> project(Foo.csproj)`. If none ran, neither does `T`.
   - **Entry-target reachability**: confirm the build actually asked for something that transitively depends on `T`. `get_children <ProjectId> kind=target` lists everything that did execute under this project.

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

When investigating Roslyn analyzers or source generators, recommend that the user rebuild with `/p:ReportAnalyzer=true` (for example `msbuild /bl /p:ReportAnalyzer=true`). This adds richer analyzer/source-generator timing and reporting under the `Csc` task, making analyzer cost and generator behavior much easier to diagnose from the binlog.

### Why is the *incremental* build doing work? (the up-to-date check)
Goal: find targets that re-ran on a no-change rebuild and shouldn't have.

If the user mainly wants to know whether a subsequent build would be up-to-date, recommend MSBuild's `/question` flag. It reports whether everything is up-to-date or whether MSBuild would do work, which is useful before collecting deeper incremental-build binlogs.

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

### Find files written more than once (double writes)
The StructuredLogger analyzer pre-computes these into a top-level `Folder` named exactly `DoubleWrites` (no DSL token, no separate tool needed).

1. `search $folder DoubleWrites` (or just `search DoubleWrites`) → one match if any double writes exist; the result id is the folder.
2. `get_children <id>` → one entry per offending file (each child is itself a folder named after the path); `count $folder under(<id>)` if you want the total quickly.
3. `get_children <fileId>` → list of source nodes (tasks / targets) that wrote it. Use `get_ancestors` on each to attribute it to a project.

Empty result = no double writes (folder is only created when at least one is detected).

### Who pulls in NuGet package `Foo` (and which version)?
The `$nuget` token traverses the synthetic NuGet dependency graph the StructuredLogger builds from embedded `project.assets.json` files. Search it for package names, versions/version ranges, direct or transitive dependencies, and files that came from NuGet packages. Each match shows the chain from a project, through any project references, down to the direct `<PackageReference>`, and on through transitive dependencies.

- `search $nuget project(Foo.csproj)` — list Foo's NuGet dependencies.
- `search $nuget Newtonsoft.Json` — every project that ends up with Newtonsoft.Json on its closure, with the version actually resolved.
- `search $nuget Newtonsoft.Json 13.0.3` — narrow to a specific version (useful when chasing a version conflict).
- `search $nuget project(Foo.csproj) Newtonsoft.Json` — restrict to one project's graph and search both dependencies and resolved packages.
- `search $nuget project(Foo.csproj) File.dll` — find a file that came from a NuGet package in one project's graph.
- `search $nuget project(.csproj) 13.0.3` — search all projects for a specific version or version range.
- `search $nuget project(.) Newtonsoft.Json 13.0.3` — `project(.)` / `project(.csproj)` matches any project; combined with a version it answers "who is forcing 13.0.3 onto someone?"

The result tree is the chain itself: `Project → ProjectReference(s) → PackageReference → transitive dependency → ... → Newtonsoft.Json 13.0.3`. Read top-to-bottom to see *why* the package landed in that project.

### Traverse the project reference graph
The `$projectreference` token surfaces the synthetic project-to-project dependency graph the StructuredLogger builds.

- `search $projectreference` — every project, with the projects it references underneath. Useful for an overall topology view.
- `search $projectreference project(Foo.csproj)` — **bidirectional**: shows both projects `Foo` references *and* projects that reference `Foo`. Answers "what touches Foo?" / "what does Foo touch?" in a single query.
- Combine with the NuGet recipe above when a transitive package shows up unexpectedly: trace the project-reference path first, then run `$nuget project(<each project on the path>) <package>` to find where it enters.

### Compare two builds (fast vs slow, succeeded vs failed)
Load both with `load_binlog`, then run the *same* query against each path. Useful staples:

- `count $error` and `count $warning` per file
- `search $task $time` with `maxResults=20` per file
- `search $target Build $time` per file
- `count $project` per file (parallelism / repeat-execution check)

Ids are not portable between the two — re-issue searches per binlog.

### When the current binlog is not rich enough
Sometimes the right answer is to ask the user for a rebuild with extra MSBuild instrumentation:

- **Property lifetime during evaluation**: set `MSBUILDLOGPROPERTYTRACKING=15` before the build, then capture a new binlog. This logs every property assignment and reassignment during evaluation.
- **Roslyn analyzers/source generators**: build with `/p:ReportAnalyzer=true` to add analyzer and generator reporting under `Csc`.
- **Evaluation performance**: in the rare case where the user needs to profile evaluation itself, ask them to pass `/profileevaluation` to MSBuild. This reveals extra perf data for each project evaluation.
- **Incremental up-to-date check**: use `msbuild /question` when the user wants to know whether a later build would be up-to-date or would do work.

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
