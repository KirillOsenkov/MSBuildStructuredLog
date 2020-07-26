using System;
using System.Collections.Generic;
using System.Text;

namespace ResourcesGenerator
{
    public class ResourcesCollection
    {
        public Dictionary<string, Dictionary<string, string>> CultureResources { get; set; } = new Dictionary<string, Dictionary<string, string>>();
    }
}
