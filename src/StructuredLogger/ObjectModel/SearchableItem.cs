using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class SearchableItem : Item
    {
        public SearchableItem() : base() { }

        public string SearchText
        {
            get { return _searchText ?? this.Text; }
            set { _searchText = value; }
        }

        private string _searchText;
    }
}
