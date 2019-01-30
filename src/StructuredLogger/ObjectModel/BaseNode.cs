namespace Microsoft.Build.Logging.StructuredLogger
{
    public abstract class BaseNode : ObservableObject
    {
        private protected NodeFlags Flags { get; private set; }

        /// <summary>
        /// Since there can only be 1 selected node at a time, don't waste an instance field
        /// just to store a bit. Store the currently selected node here and this way we save
        /// 4 bytes per instance (due to layout/alignment). This is huge savings for large 
        /// trees.
        /// </summary>
        private static BaseNode selectedNode = null;

        public bool IsSelected
        {
            get
            {
                return selectedNode == this;
            }

            set
            {
                if (IsSelected == value)
                {
                    RaisePropertyChanged();
                    return;
                }

                selectedNode = value ? this : null;

                RaisePropertyChanged();
                RaisePropertyChanged("IsLowRelevance");
            }
        }

        public bool IsSearchResult
        {
            get => HasFlag(NodeFlags.SearchResult);

            set
            {
                if (IsSearchResult == value)
                {
                    return;
                }

                SetFlag(NodeFlags.SearchResult, value);
                RaisePropertyChanged();
            }
        }

        public bool ContainsSearchResult
        {
            get => HasFlag(NodeFlags.ContainsSearchResult);

            set
            {
                if (ContainsSearchResult == value)
                {
                    return;
                }

                SetFlag(NodeFlags.ContainsSearchResult, value);
                RaisePropertyChanged();
            }
        }

        private protected bool HasFlag(NodeFlags flag) => (Flags & flag) == flag;

        private protected void SetFlag(NodeFlags flag, bool value)
        {
            if (value)
            {
                Flags = Flags | flag;
            }
            else
            {
                Flags = Flags & ~flag;
            }
        }
    }
}
