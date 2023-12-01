ObservableObject
    BaseNode
        NameValueNode
            Metadata
            Property
        TreeNode
            EvaluationProfileEntry
            NamedNode
                AddItem
                EntryTarget
                Folder
                Item
                Package
                Parameter
                RemoveItem
                SourceFile
                TimedNode
                    Build
                    Project
                    ProjectEvaluation
                    Target
                    Task
                        CopyTask
                            RobocopyTask
                        ManagedCompilerTask
                            VbcTask
                            CscTask
                            FscTask
                        MSBuildTask
                        ResolveAssemblyReferenceTask
            SourceFileLine
            TextNode
                AbstractDiagnostic
                    CriticalBuildMessage
                    Error
                        BuildError
                    Warning
                Import
                Message
                    MessageWithLocation
                    TimedMessage
                NoImport
                Note
                ProxyNode

digraph {
  AddItem -> Item
  Build -> Folder
  Build -> Message
  Build -> Project
  Build -> Property
  Build -> TimedNode
  CopyTask -> Folder
  CopyTask -> Message
  CscTask -> Folder
  CscTask -> Message
  CscTask -> Property
  Folder -> AddItem
  Folder -> EntryTarget
  Folder -> Error
  Folder -> Folder
  Folder -> Item
  Folder -> Message
  Folder -> Note
  Folder -> Package
  Folder -> Parameter
  Folder -> Property
  Folder -> Warning
  FscTask -> Folder
  FscTask -> Message
  FscTask -> Property
  Import -> Import
  Import -> NoImport
  Item -> Item
  Item -> Message
  Item -> Metadata
  MSBuildTask -> Folder
  MSBuildTask -> Project
  Parameter -> Item
  Parameter -> Metadata
  Project -> Folder
  Project -> Message
  Project -> Project
  Project -> Target
  ProjectEvaluation -> Folder
  ProjectEvaluation -> Message
  ProjectEvaluation -> Property
  ProjectEvaluation -> TimedNode
  RemoveItem -> Item
  ResolveAssemblyReferenceTask -> Folder
  ResolveAssemblyReferenceTask -> Property
  Target -> AddItem
  Target -> VbcTask
  Target -> CopyTask
  Target -> CscTask
  Target -> Folder
  Target -> FscTask
  Target -> Message
  Target -> MSBuildTask
  Target -> Property
  Target -> RemoveItem
  Target -> ResolveAssemblyReferenceTask
  Target -> Task
  Target -> VbcTask
  Task -> Folder
  Task -> Message
  Task -> Project
  Task -> Property
  TimedNode -> Folder
  TimedNode -> Import
  TimedNode -> Message
  TimedNode -> NoImport
  TimedNode -> ProjectEvaluation
  Warning -> Item
  VbcTask -> Folder
  VbcTask -> Message
  VbcTask -> Property
}