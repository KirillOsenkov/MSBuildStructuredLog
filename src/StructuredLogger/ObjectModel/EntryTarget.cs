#nullable enable

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class EntryTarget : NamedNode, IHasRelevance
    {
        private Project? project;
        private Target? target;
        public override string TypeName => nameof(EntryTarget);
        private Project? Project => project ??= GetNearestParent<Project>();
        public Target? Target => target ??= Project?.FindFirstChild<Target>(t => t.Name == Name);
        public bool IsLowRelevance => Target?.IsLowRelevance ?? true;
        public string? DurationText => Target?.DurationText;
    }
}