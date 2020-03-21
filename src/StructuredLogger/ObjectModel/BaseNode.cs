using System.Runtime.CompilerServices;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public abstract class BaseNode : ObservableObject
    {
        private NodeFlags flags;

        /// <summary>
        /// Since there can only be 1 selected node at a time, don't waste an instance field
        /// just to store a bit. Store the currently selected node here and this way we save
        /// 4 bytes per instance (due to layout/alignment). This is huge savings for large 
        /// trees.
        /// </summary>
        private static BaseNode selectedNode = null;

        public bool IsSelected
        {
            get => selectedNode == this;
            set
            {
                if (IsSelected == value)
                {
                    RaisePropertyChanged();
                    return;
                }

                selectedNode = value && IsSelectable ? this : null;

                RaisePropertyChanged();
                RaisePropertyChanged("IsLowRelevance");
            }
        }

        protected virtual bool IsSelectable => true;

        public bool IsSearchResult
        {
            get => HasFlag(NodeFlags.SearchResult);
            set => SetFlag(NodeFlags.SearchResult, value);
        }

        public bool ContainsSearchResult
        {
            get => HasFlag(NodeFlags.ContainsSearchResult);
            set => SetFlag(NodeFlags.ContainsSearchResult, value);
        }

        private protected bool HasFlag(NodeFlags flag) => (flags & flag) == flag;

        private protected void SetFlag(NodeFlags flag, bool isSet, [CallerMemberName] string propertyName = null)
        {
            var newFlags = isSet
                ? flags | flag
                : flags & ~flag;

            if (flags == newFlags)
            {
                return;
            }

            flags = newFlags;
            RaisePropertyChanged(propertyName);
        }
    }
}
