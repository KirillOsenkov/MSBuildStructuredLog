using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class CppAnalyzer
    {
        public class CppTimedNode
        {
            public DateTime StartTime;
            public DateTime EndTime;
            public string Text;
            public int NodeId;
            public BaseNode Node;
        }

        public class CppAnalyzerNode : NamedNode
        {
            CppAnalyzer cppAnalyzer;

            public CppAnalyzerNode(CppAnalyzer cppAnalyzer)
            {
                this.IsVisible = false;
                this.cppAnalyzer = cppAnalyzer;
            }

            public CppAnalyzer GetCppAnalyzer() => cppAnalyzer;
        }

        // parse for:
        // midl.exe will run on 8 out of 8 file(s) in 8 batches.  Startup phase took 46.0028ms.
        // Task 'CodeStore.idl' took 2244ms.
        // Cleanup phase took 68ms.
        const string regexStartupPhase = @"^.*\.exe will run on (?'activeFiles'[0-9]*) out of (?'totalFiles'[0-9]*) file\(s\) in [0-9]* batches\.\s*Startup phase took (?'msTime'[0-9]*.[0-9]*)ms.$";
        readonly Regex startupPhase = new Regex(regexStartupPhase, RegexOptions.Multiline);
        const string regexCleanupPhase = @"^Cleanup phase took (?'msTime'[0-9]*.[0-9]*)ms.$";
        readonly Regex cleanupPhase = new Regex(regexCleanupPhase, RegexOptions.Multiline);
        const string regexTaskTime = @"^Task '(?'filename'.*)' took (?'msTime'([0-9]*\.[0-9]+|[0-9]+))ms.$";
        readonly Regex TaskTime = new Regex(regexTaskTime, RegexOptions.Multiline);

        // time(C:\Program Files (x86)\Microsoft Visual Studio\2019\Preview\VC\Tools\MSVC\14.24.28218\bin\Hostx86\x86\c1xx.dll)=0.83512s < 985096605139 - 985104956295 > BB [C:\Users\yuehuang\AppData\Local\Temp\123\main36.cpp]
        // time(C:\Program Files(x86)\Microsoft Visual Studio\2019\Preview\VC\Tools\MSVC\14.24.28218\bin\Hostx86\x86\c2.dll)=0.01935s < 985104875296 - 985105068765 > BB[C: \Users\yuehuang\AppData\Local\Temp\123\main47.cpp]
        const string regexBTPlus = @"^time\(.*(c1xx\.dll|c2\.dll)\)=(?'msTime'([0-9]*\.[0-9]+|[0-9]+))s \< (?'startTime'[\d]*) - (?'endTime'[\d]*) \>\s*BB\s*\[(?'filename'[^\]]*)\]$";
        readonly Regex BTPlus = new Regex(regexBTPlus, RegexOptions.Multiline);

        // Lib: Final Total time = 0.00804s < 5881693617253 - 5881693697673 > PB: 143409152 [D:\test\ConsoleApplication2\x64\Debug\ConsoleApplication2.lib] 
        // note: there is trailing white space from the tool
        const string regexLibFinalTime = @"^Lib: Final Total time = (?'msTime'([0-9]*\.[0-9]+|[0-9]+))s \< (?'startTime'[\d]*) - (?'endTime'[\d]*) \>\s*PB:\s*[\d]* \[(?'filename'[^\]]*)\]\s*$";
        readonly Regex libFinalTime = new Regex(regexLibFinalTime, RegexOptions.Multiline);

        //  Pass 1: Interval #1, time = 0.125s
        //    Wait PDB close: Total time = 0.000s
        //    Wait type merge: Total time = 0.000s
        //  Pass 2: Interval #2, time = 0.016s
        //  Final: Total time = 0.141s
        const string regexLinkTotalTime = @"^Final: Total time = (?'msTime'[0-9]*.[0-9]*)s$";
        readonly Regex linkTotalTime = new Regex(regexLinkTotalTime, RegexOptions.Multiline);

        private const string MultiToolTaskName = "MultiToolTask";
        private const string CLTaskName = "CL";
        private const string LinkTaskName = "Link";
        private const string LibTaskName = "LIB";
        private const string filenameRegexMatchName = "filename";
        private const string startTimeRegexMatchName = "startTime";
        private const string endTimeRegexMatchName = "endTime";
        private const string msTimeRegexMatchName = "msTime";
        private const string btplusKeyword = @"time(";
        private const string mttKeyword = " took ";
        private const string mttCleanUpKeyword = "Cleanup phase took ";
        private const string mttStartUpKeyword = "will run on ";
        private const string libKeyword = "Lib: Final Total time =";
        private const string linkKeyword = "Final: Total time =";
        private bool globalBtplus = false;
        private bool globalLibTime = false;
        private bool globalLinkTime = false;

        private TimeSpan oneMilliSecond = TimeSpan.FromMilliseconds(1);
        private static HashSet<string> hashCppTasks = new HashSet<string>() { MultiToolTaskName, CLTaskName, LinkTaskName, LibTaskName };

        List<CppTimedNode> resultTimedNode = new List<CppTimedNode>();

        public CppAnalyzer()
        {
            globalBtplus = false;
            globalLibTime = false;
            globalLinkTime = false;
        }

        public void AnalyzeEnvironment(NamedNode node)
        {
            // Search for /Bt+ and /TIME in the environment variables
            node.VisitAllChildren<Property>(envProperty =>
            {
                if (envProperty.Name == "_CL_" || envProperty.Name == "__CL__")
                {
                    globalBtplus = envProperty.Value.Contains("/Bt+");
                }
                else if (envProperty.Name == "_LINK_" || envProperty.Name == "__LINK__")
                {
                    globalLinkTime = envProperty.Value.Contains("/TIME");
                }
                else if (envProperty.Name == "_LIB_" || envProperty.Name == "__LIB__")
                {
                    globalLibTime = envProperty.Value.Contains("/TIME");
                }
            });
        }

        public static bool IsCppTask(string taskName)
        {
            return hashCppTasks.Contains(taskName);
        }

        public void AppendCppAnalyzer(Build build)
        {
            build.AddChild(new CppAnalyzerNode(this));
        }

        public IEnumerable<CppTimedNode> GetAnalyzedTimedNode()
        {
            return resultTimedNode;
        }

        public void AnalyzeTask(Task cppTask)
        {
            // MultiToolTask batches tasks and runs them in parallel.
            // For this view, de-batch them into individual task units.
            if ((cppTask.Name == MultiToolTaskName || cppTask.Name == CLTaskName) && cppTask.HasChildren)
            {
                bool usingBTTime = globalBtplus;
                List<CppTimedNode> blocks = new List<CppTimedNode>();
                DateTime mttStartupTime = cppTask.StartTime;
                DateTime mttCleanupTime = cppTask.EndTime;

                foreach (var child in cppTask.Children)
                {
                    if (child is TimedMessage message)
                    {
                        DateTime endTime = DateTime.MinValue;
                        DateTime startTime = DateTime.MinValue;
                        string messageText = message.Text;

                        if ((usingBTTime && message.Text.StartsWith(btplusKeyword)) || (!usingBTTime && message.Text.Contains(mttKeyword)))
                        {
                            Match match = usingBTTime ? BTPlus.Match(message.Text) : TaskTime.Match(message.Text);
                            if (match.Success)
                            {
                                if (usingBTTime)
                                {
                                    // Matching Bt+
                                    string filename = match.Groups[filenameRegexMatchName].Value;
                                    string startTimeValue = match.Groups[startTimeRegexMatchName].Value;
                                    string endTimeValue = match.Groups[endTimeRegexMatchName].Value;
                                    if (long.TryParse(startTimeValue, out long tryStartTime) && long.TryParse(endTimeValue, out long tryEndTime) && !string.IsNullOrWhiteSpace(filename))
                                    {
                                        startTime = new DateTime(tryStartTime);
                                        endTime = new DateTime(tryEndTime);
                                        messageText = Path.GetFileName(filename);
                                    }
                                }
                                else
                                {
                                    // MTT messages only print duration, assume that timestamp of the message is the end.
                                    // Round 1ms from start time so that the graph fits better.
                                    string filename = match.Groups[filenameRegexMatchName].Value;
                                    string msTime = match.Groups[msTimeRegexMatchName].Value;
                                    if (double.TryParse(msTime, out double tryValue) && !string.IsNullOrWhiteSpace(filename))
                                    {
                                        startTime = message.Timestamp - TimeSpan.FromMilliseconds(tryValue) + oneMilliSecond;
                                        endTime = message.Timestamp;
                                        messageText = Path.GetFileName(filename);
                                    }
                                }

                                if (startTime > DateTime.MinValue)
                                {
                                    var block = new CppTimedNode()
                                    {
                                        StartTime = startTime,
                                        EndTime = endTime,
                                        Text = messageText,
                                        Node = message,
                                        NodeId = cppTask.NodeId,
                                    };
                                    blocks.Add(block);
                                }
                            }
                        }
                        else if (cppTask.Name == MultiToolTaskName && message.Text.Contains(mttCleanUpKeyword) || message.Text.Contains(mttStartUpKeyword))
                        {
                            var match = startupPhase.Match(message.Text);
                            if (match.Success)
                            {
                                string msTime = match.Groups[msTimeRegexMatchName].Value;
                                if (double.TryParse(msTime, out double tryValue))
                                {
                                    startTime = message.Timestamp - TimeSpan.FromMilliseconds(tryValue) + oneMilliSecond;
                                    endTime = message.Timestamp;
                                    mttStartupTime = message.Timestamp;
                                }
                            }
                            else
                            {
                                match = cleanupPhase.Match(message.Text);
                                string msTime = match.Groups[msTimeRegexMatchName].Value;
                                if (double.TryParse(msTime, out double tryValue))
                                {
                                    startTime = message.Timestamp - TimeSpan.FromMilliseconds(tryValue) + oneMilliSecond;
                                    endTime = message.Timestamp;
                                    mttCleanupTime = message.Timestamp - TimeSpan.FromMilliseconds(tryValue);
                                }
                            }

                            if (startTime > DateTime.MinValue)
                            {
                                var block = new CppTimedNode()
                                {
                                    StartTime = startTime,
                                    EndTime = endTime,
                                    Text = messageText,
                                    Node = message,
                                    NodeId = cppTask.NodeId,
                                };

                                // Add these messages to directly to the lane as to avoid mixing with the Bt+ messages.
                                resultTimedNode.Add(block);
                            }
                        }
                    }

                    if (!usingBTTime && child is Property property)
                    {
                        if (property.Name == Strings.CommandLineArguments && property.Value.Contains("/Bt+"))
                        {
                            usingBTTime = true;
                        }
                    }
                }

                if (usingBTTime && blocks.Count > 0 && mttStartupTime != DateTime.MinValue && mttCleanupTime != DateTime.MinValue)
                {
                    // BT+ timestamp is not a global time, but is relative to the first instance (see QueryPerformanceCounter)
                    // so compute the offset and center the Bt+ graph.  Assume that tool's startup closing time are the same.
                    DateTime offset = blocks.Min(p => p.StartTime);
                    TimeSpan totalDuration = blocks.Max(p => p.EndTime) - offset;
                    var mttDuration = mttCleanupTime - mttStartupTime;
                    if (totalDuration > TimeSpan.Zero && totalDuration < mttDuration)
                    {
                        mttStartupTime += TimeSpan.FromTicks((mttDuration - totalDuration).Ticks / 2);
                        foreach (CppTimedNode block in blocks)
                        {
                            block.StartTime = mttStartupTime + block.StartTime.Subtract(offset);
                            block.EndTime = mttStartupTime + block.EndTime.Subtract(offset);
                        }
                    }
                    else
                    {
                        // unable to put the nodes in the center.  Just put it right up against the mtt startup time
                        foreach (CppTimedNode block in blocks)
                        {
                            block.StartTime = mttStartupTime + block.StartTime.Subtract(offset);
                            block.EndTime = mttStartupTime + block.EndTime.Subtract(offset);
                        }
                    }
                }

                resultTimedNode.AddRange(blocks);
            }
            else if (cppTask.Name == LibTaskName && cppTask.HasChildren)
            {
                bool usingLibTime = globalLibTime;

                foreach (var child in cppTask.Children)
                {
                    if (usingLibTime && child is TimedMessage message && message.Text.Contains(libKeyword))
                    {
                        DateTime endTime = DateTime.MinValue;
                        DateTime startTime = DateTime.MinValue;
                        string messageText = message.Text;

                        var match = libFinalTime.Match(message.Text);
                        if (match.Success)
                        {
                            string filename = match.Groups[filenameRegexMatchName].Value;
                            string startTimeValue = match.Groups[startTimeRegexMatchName].Value;
                            string endTimeValue = match.Groups[endTimeRegexMatchName].Value;
                            if (long.TryParse(startTimeValue, out long tryStartTime) && long.TryParse(endTimeValue, out long tryEndTime) && !string.IsNullOrWhiteSpace(filename))
                            {
                                var duration = tryEndTime - tryStartTime;
                                startTime = message.Timestamp - TimeSpan.FromTicks(duration);
                                endTime = message.Timestamp;
                                messageText = Path.GetFileName(filename);
                            }
                        }

                        if (startTime > DateTime.MinValue)
                        {
                            var block = new CppTimedNode()
                            {
                                StartTime = startTime,
                                EndTime = endTime,
                                Text = messageText,
                                Node = message,
                                NodeId = cppTask.NodeId,
                            };
                            resultTimedNode.Add(block);
                        }
                    }
                    else if (!usingLibTime && child is Property property)
                    {
                        if (property.Name == Strings.CommandLineArguments && property.Value.Contains("/TIME"))
                        {
                            usingLibTime = true;
                        }
                    }
                }
            }
            else if (cppTask.Name == LinkTaskName && cppTask.HasChildren)
            {
                bool usingLinkTime = globalLinkTime;

                foreach (var child in cppTask.Children)
                {
                    if (child is TimedMessage message && message.Text.Contains(linkKeyword))
                    {
                        DateTime endTime = DateTime.MinValue;
                        DateTime startTime = DateTime.MinValue;
                        string messageText = message.Text;

                        var match = linkTotalTime.Match(message.Text);
                        if (match.Success)
                        {
                            string secTime = match.Groups[msTimeRegexMatchName].Value;
                            if (double.TryParse(secTime, out double trySecTime))
                            {
                                startTime = message.Timestamp - TimeSpan.FromSeconds(trySecTime);
                                endTime = message.Timestamp;
                            }
                        }

                        if (startTime > DateTime.MinValue)
                        {
                            var block = new CppTimedNode()
                            {
                                StartTime = startTime,
                                EndTime = endTime,
                                Text = messageText,
                                Node = message,
                                NodeId = cppTask.NodeId,
                            };
                            resultTimedNode.Add(block);
                        }
                    }
                    else if (!usingLinkTime && child is Property property)
                    {
                        if (property.Name == Strings.CommandLineArguments && property.Value.Contains("/TIME"))
                        {
                            usingLinkTime = true;
                        }
                    }
                }
            }
        }
    }
}
