using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class VbcTask : Task
    {
        private CompilationWrites? compilationWrites;

        public CompilationWrites? CompilationWrites
        {
            get
            {
                if (!HasChildren)
                {
                    return null;
                }

                if (!compilationWrites.HasValue)
                {
                    compilationWrites = Logging.StructuredLogger.CompilationWrites.TryParse(this);
                }

                return compilationWrites.Value;
            }
        }
    }
}
