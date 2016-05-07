using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class JsonLogWriter
    {
        public void SaveToJson(string logFile)
        {
            var text = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(logFile, text);
        }
    }
}
