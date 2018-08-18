using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StructuredLogViewer.Core
{
    public interface IMSBuildLocator
    {
        string[] GetMSBuildLocations();
    }
}
