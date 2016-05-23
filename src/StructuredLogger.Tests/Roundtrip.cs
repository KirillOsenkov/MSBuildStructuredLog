using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Build.Logging.StructuredLogger;
using StructuredLogViewer;
using Xunit;

namespace StructuredLogger.Tests
{
    public class Roundtrip
    {
        //[Fact]
        public void RoundtripTest()
        {
            var file = @"D:\1.xml";
            var build = XmlLogReader.ReadFromXml(file);
            var newName = Path.ChangeExtension(file, ".new.xml");
            XmlLogWriter.WriteToXml(build, newName);
            Process.Start("devenv", $"/diff \"{file}\" \"{newName}\"");
        }

        //[Fact]
        public void SearchPerf()
        {
            var file = @"D:\contentsync.xml";
            var build = XmlLogReader.ReadFromXml(file);
            var sw = Stopwatch.StartNew();
            var search = new Search(build);
            var results = search.FindNodes("test");
            var elapsed = sw.Elapsed;
            MessageBox.Show(elapsed.ToString());
            File.WriteAllLines(@"D:\2.txt", results.Select(r => r.Field).ToArray());
        }
    }
}
