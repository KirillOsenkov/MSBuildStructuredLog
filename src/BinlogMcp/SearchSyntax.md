MSBuild Structured Log Viewer search query syntax
==================================================

A query is a list of space-separated terms. All terms must match (AND).
Matching is substring and case-insensitive by default. Use double quotes for
literal/exact matching:

  Copying file               substring match for the two words 'copying' AND 'file'
  "Copying file"             literal substring match for the exact phrase
  "Copy"                     exact (whole-string, case-insensitive) match — quotes around
                             a single word turn off substring search

Node-kind filters
-----------------
Prefix a `$kind` token to restrict results to a particular node type:

  $project          Project nodes
  $projectevaluation Project evaluation nodes
  $target           Target nodes
  $task             Task invocations
  $error            Errors
  $warning          Warnings
  $message          Messages
  $property         Property nodes
  $item             Item nodes
  $additem          AddItem nodes
  $removeitem       RemoveItem nodes
  $metadata         Metadata nodes
  $import           Import nodes (resolved imports)
  $noimport         NoImport nodes (failed/skipped imports)
  $secret           Detected secrets (if redaction is enabled)

Aliases (expanded internally):
  $csc   → $task csc
  $rar   → $task ResolveAssemblyReference

Find a node by its index (TimedNode.Index):
  $42               the node with Index 42

Find by evaluation id
----------------------------------------------------------
ProjectEvaluation titles end with `id:N`, where N is the evaluation id.
Project execution nodes cross-reference their evaluation in a message that
contains `id:N`. So you can correlate a Project execution with its
ProjectEvaluation (and vice versa) by searching for the literal `id:N`:

  id:29              every node whose text contains 'id:29' — finds both
                     the ProjectEvaluation with that id AND the Project
                     execution(s) that reference it.

Combine with `$projectevaluation` to get just the evaluation node:

  $projectevaluation id:29

Field filters
-------------
For NameValue-style nodes (Property, Metadata, Item):

  name=Configuration              name contains 'Configuration' (substring)
  name="Configuration"            name equals 'Configuration' exactly
  value=Debug                     value contains 'Debug'
  name=Foo value=Bar              both filters apply

Other field filters:
  skipped=true                    only skipped targets
  skipped=false                   exclude skipped targets
  height=0                        projects with no project references
  height=N                        projects whose deepest reference chain is N
  height=max                      tallest project(s); also reports the max value

Hierarchical scoping
--------------------
  under(QUERY)         match only nodes whose ancestor chain contains a node matching QUERY
  notunder(QUERY)      exclude nodes whose ancestor chain contains a match for QUERY
  project(QUERY)       like under(), but only matches the NEAREST parent project
  not(QUERY)           exclude nodes that themselves match QUERY

QUERY may be any nested query, including more under()/notunder()/project()/not().
Multiple under() / project() / notunder() clauses are unioned.

Examples:
  $csc under($project Core)                       Csc invocations anywhere under a project named *Core*
  $error project(MyApp)                           errors whose nearest parent project matches MyApp
  $warning notunder($task Csc)                    warnings not produced by a Csc task
  $rar under(A) notunder(B) notunder(Evaluation)  RAR tasks under A but not under B and not under any Evaluation
  $metadata name=CopyToOutputDirectory value=PreserveNewest under($item .txt under($additem None))

Time intervals (filter)
-----------------------
Filter Project / Target / Task results by start or end timestamp. Timestamp
must be quoted; any DateTime.Parse-able format works.

  start<"2024-01-15 14:30:00"    started before that timestamp
  start>"2024-01-15 14:30:00"    started after
  end<"2024-01-15 14:30:00.500"  ended before
  end>"2024-01-15 14:30:00.500"  ended after

Time annotations (display + sort)
---------------------------------
Append these flags to include extra fields in results and sort accordingly.
They are NOT filters — they don't reduce the result set.

  $time      include duration; sort by duration descending (alias: $duration)
  $start     include start time; sort by start time (alias: $starttime)
  $end       include end time; sort by end time (alias: $endtime)

Examples:
  $task $time                                     all tasks, slowest first
  $target $time under($project MyApp)             targets in MyApp, slowest first
  $project $start                                 projects in start-time order

Specialized indexes
-------------------
$copy   — file-copy map
  $copy filename                       all copies whose source or dest contains 'filename'
  $copy directory\path                 all copies in or out of that directory
  $copy C:\full\file\path              all copies involving the specific file

$nuget   — NuGet / project.assets.json
  $nuget project(MyProject.csproj)            list MyProject's NuGet dependencies
  $nuget project(MyProject.csproj) Pkg.Name   search dependencies, resolved packages and files
  $nuget project(.csproj) 13.0.3              search across all projects (slow); version-aware
  $nuget project(MyProject.csproj) File.dll   find a file from a NuGet package

$projectreference   — project reference graph
  $projectreference project(MyProject.csproj) RefProj   projects referenced by MyProject.csproj directly
                                                        or indirectly (and matching RefProj). When the
                                                        scope narrows to a single project, the projects
                                                        REFERENCING it are also shown — bidirectional.
  $projectreference project(App.csproj)                 transitive closure referenced by App.csproj
  $projectreference project(App.csproj) Core            filter to references whose name contains Core
  $project height=N                                     projects at a given level in the reference graph

Tips
----
* The query parser is whitespace-tolerant: `under (` and `under(` both work.
  Same for `not (`, `notunder (`, `project (`, `start <`, `name =`, etc.
* Substring matches are very broad; use $kind filters to narrow scope first.
* Use `under()` / `notunder()` instead of trying to express scope via free text.
* Quoted single words ("Csc") give whole-string matches — useful when a
  short token would otherwise collide with longer strings.
* `$NN` (e.g. `$1234`) returns the single node with that TimedNode.Index.
