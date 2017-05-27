namespace Microsoft.Build.Logging.StructuredLogger
{
    public class Folder : NamedNode, IHasRelevance
    {
        private bool isLowRelevance = false;
        public bool IsLowRelevance
        {
            get
            {
                return isLowRelevance && !IsSelected;
            }

            set
            {
                if (isLowRelevance == value)
                {
                    return;
                }

                isLowRelevance = value;
                RaisePropertyChanged();
            }
        }
    }
}
