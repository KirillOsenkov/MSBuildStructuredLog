using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Logging
{
    public class TaskItem : ITaskItem2
    {
        public string ItemSpec { get; set; }
        public Dictionary<string, string> Metadata { get; } = new Dictionary<string, string>();
        public string EvaluatedIncludeEscaped { get; set; }

        public int MetadataCount => Metadata.Count;
        public ICollection MetadataNames => Metadata.Keys;

        public TaskItem()
        {
        }

        public TaskItem(string itemSpec)
        {
            ItemSpec = itemSpec;
        }

        public IDictionary CloneCustomMetadata()
        {
            return Metadata;
        }

        public IDictionary CloneCustomMetadataEscaped()
        {
            throw new NotImplementedException();
        }

        public void CopyMetadataTo(ITaskItem destinationItem)
        {
            foreach (var kvp in Metadata)
            {
                destinationItem.SetMetadata(kvp.Key, kvp.Value);
            }
        }

        public string GetMetadata(string metadataName)
        {
            if (!Metadata.TryGetValue(metadataName, out var result))
            {
                if (metadataName == "FullPath")
                {
                    return ItemSpec;
                }
            }

            return result ?? "";
        }

        public string GetMetadataValueEscaped(string metadataName)
        {
            throw new NotImplementedException();
        }

        public void RemoveMetadata(string metadataName)
        {
            throw new NotImplementedException();
        }

        public void SetMetadata(string metadataName, string metadataValue)
        {
            Metadata[metadataName] = metadataValue;
        }

        public void SetMetadataValueLiteral(string metadataName, string metadataValue)
        {
            SetMetadata(metadataName, metadataValue);
        }

        public override string ToString() => ItemSpec;
    }
}
