using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
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
            foreach (var file in Directory.GetFiles(@"D:\XmlBuildLogs", "*.xml", SearchOption.AllDirectories).ToArray())
            {
                var build = XlinqLogReader.ReadFromXml(file);
                var newName = Path.ChangeExtension(file, ".new.xml");
                XmlLogWriter.WriteToXml(build, newName);
                var source = File.ReadAllText(file);
                var destination = File.ReadAllText(newName);
                if (source != destination)
                {
                    Process.Start("devenv", $"/diff \"{file}\" \"{newName}\"");
                    break;
                }
                else
                {
                    File.Delete(newName);
                }
            }
        }

        //[Fact]
        public void SearchPerf()
        {
            var file = @"D:\contentsync.xml";
            var build = XlinqLogReader.ReadFromXml(file);
            var sw = Stopwatch.StartNew();
            var search = new Search(build);
            var results = search.FindNodes("test");
            var elapsed = sw.Elapsed;
            MessageBox.Show(elapsed.ToString());
            File.WriteAllLines(@"D:\2.txt", results.Select(r => r.Field).ToArray());
        }
    }
}
