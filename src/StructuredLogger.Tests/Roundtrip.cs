using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Build.Logging.StructuredLogger;
using Xunit;
using Microsoft.Build.Logging;
using Xunit.Abstractions;

namespace StructuredLogger.Tests
{
    public class Roundtrip
    {
        private readonly ITestOutputHelper output;

        public Roundtrip(ITestOutputHelper testOutputHelper)
        {
            this.output = testOutputHelper;
        }

        //[Fact]
        public void RoundtripTest()
        {
            foreach (var file in Directory.GetFiles(@"D:\XmlBuildLogs", "*.xml", SearchOption.AllDirectories).ToArray())
            {
                var build = Serialization.Read(file);
                var newName = Path.ChangeExtension(file, ".new.xml");
                Serialization.Write(build, newName);
                if (Differ.AreDifferent(file, newName))
                {
                    break;
                }
                else
                {
                    File.Delete(newName);
                }

                newName = Path.ChangeExtension(file, ".buildlog");
                Serialization.Write(build, newName);
                build = Serialization.Read(newName);
                newName = Path.ChangeExtension(file, ".new2.xml");
                Serialization.Write(build, newName);
                if (Differ.AreDifferent(file, newName))
                {
                    break;
                }
                else
                {
                    File.Delete(newName);
                    //File.Delete(Path.ChangeExtension(file, ".buildlog"));
                }
            }
        }

        //[Fact]
        //public void SearchPerf()
        //{
        //    var file = @"D:\contentsync.xml";
        //    var build = Serialization.Read(file);
        //    var sw = Stopwatch.StartNew();
        //    var search = new Search(build);
        //    var results = search.FindNodes("test");
        //    var elapsed = sw.Elapsed;
        //    MessageBox.Show(elapsed.ToString());
        //    File.WriteAllLines(@"D:\2.txt", results.Select(r => r.Field).ToArray());
        //}

        //[Fact]
        public void ReadBinaryLogRecords()
        {
            var reader = new BinLogReader();
            var records = reader.ReadRecords(@"C:\temp\msbuild.binlog");
            foreach (var record in records)
            {
                var t = record.Args;
            }
        }

        //[Fact]
        public void LoadBinlog()
        {
            var sw = Stopwatch.StartNew();
            var build = BinaryLog.ReadBuild(@"C:\temp\vsmac.binlog");
            var elapsed = sw.Elapsed;
            output.WriteLine(elapsed.ToString());
        }
    }
}
