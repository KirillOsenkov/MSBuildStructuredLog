using System;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class Message : TextNode
    {
        public DateTime Timestamp { get; set; }

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
