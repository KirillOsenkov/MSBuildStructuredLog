using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Build.Logging.StructuredLogger;
using static System.Reflection.Metadata.BlobBuilder;

namespace StructuredLogViewer
{
    public class Timeline
    {
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
        const string regexLinkPass1 = @"^Cleanup phase took (?'msTime'[0-9]*.[0-9]*)ms.$";
        readonly Regex linkPass1 = new Regex(regexLinkPass1, RegexOptions.Multiline);
        const string regexLinkPass2 = @"^Cleanup phase took (?'msTime'[0-9]*.[0-9]*)ms.$";
        readonly Regex linkPass2 = new Regex(regexLinkPass2, RegexOptions.Multiline);

        private bool globalBtplus = false;
        private bool globalLibTime = false;
        private bool globalLinkTime = false;

        public Dictionary<int, Lane> Lanes { get; set; } = new Dictionary<int, Lane>();

        public Timeline(Build build, bool includeCpp)
        {
            Populate(build, includeCpp);

            globalBtplus = false;
            globalLibTime = false;
            globalLinkTime = false;
        }

        private void Populate(Build build, bool includeCpp = false)
        {
            build.VisitAllChildren<TimedNode>(node =>
            {
                if (node is Build)
                {
                    if (includeCpp)
                    {
                        // Search for Global /Bt+ and /TIME
                        node.FindFirstChild<Folder>(childNode =>
                        {
                            if (childNode.Name == "Environment")
                            {
                                childNode.VisitAllChildren<Property>(envProperty =>
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
                            return false;
                        });
                    }

                    return;
                }

                if (node is Microsoft.Build.Logging.StructuredLogger.Task task &&
                    (string.Equals(task.Name, "MSBuild", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(task.Name, "CallTarget", StringComparison.OrdinalIgnoreCase)))
                {
                    return;
                }

                var nodeId = node.NodeId;
                if (!Lanes.TryGetValue(nodeId, out var lane))
                {
                    lane = new Lane();
                    Lanes[nodeId] = lane;
                }

                if (includeCpp)
                {
                    IEnumerable<Block> blocks = PopulateCppNodes(node);
                    lane.AddRange(blocks);
                }

                lane.Add(CreateBlock(node));
            });
        }

        private IEnumerable<Block> PopulateCppNodes(TimedNode node)
        {
            TimeSpan oneMilliSecond = TimeSpan.FromMilliseconds(1);
            List<Block> resultBlocks = new List<Block>();

            // MultiToolTask batches tasks and runs them in parallel.
            // For this view, de-batch them into individual task units.
            if (node is Microsoft.Build.Logging.StructuredLogger.Task cppTask && (cppTask.Name == "MultiToolTask" || cppTask.Name == "CL") && cppTask.HasChildren)
            {
                bool usingBTTime = globalBtplus;
                List<Block> blocks = new List<Block>();
                DateTime mttPostStartupTime = DateTime.MinValue;

                foreach (var child in cppTask.Children)
                {
                    if (child is Message message)
                    {
                        DateTime endTime = DateTime.MinValue;
                        DateTime startTime = DateTime.MinValue;
                        string messageText = message.Text;

                        var match = usingBTTime ? BTPlus.Match(message.Text) : TaskTime.Match(message.Text);
                        if (match.Success)
                        {
                            if (usingBTTime)
                            {
                                // Matching Bt+
                                string filename = match.Groups["filename"].Value;
                                string startTimeValue = match.Groups["startTime"].Value;
                                string endTimeValue = match.Groups["endTime"].Value;
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
                                string filename = match.Groups["filename"].Value;
                                string msTime = match.Groups["msTime"].Value;
                                if (double.TryParse(msTime, out double tryValue) && !string.IsNullOrWhiteSpace(filename))
                                {
                                    startTime = message.Timestamp - TimeSpan.FromMilliseconds(tryValue) + oneMilliSecond;
                                    endTime = message.Timestamp;
                                    messageText = Path.GetFileName(filename);
                                }
                            }

                            if (startTime > DateTime.MinValue)
                            {
                                var block = new Block()
                                {
                                    StartTime = startTime,
                                    EndTime = endTime,
                                    Text = messageText,
                                    Node = message,
                                };
                                blocks.Add(block);
                            }
                        }
                        else if (node.Name == "MultiToolTask")
                        {
                            match = startupPhase.Match(message.Text);
                            if (match.Success)
                            {
                                string msTime = match.Groups["msTime"].Value;
                                if (double.TryParse(msTime, out double tryValue))
                                {
                                    startTime = message.Timestamp - TimeSpan.FromMilliseconds(tryValue) + oneMilliSecond;
                                    endTime = message.Timestamp;
                                    mttPostStartupTime = message.Timestamp;
                                }
                            }
                            else
                            {
                                match = cleanupPhase.Match(message.Text);
                                string msTime = match.Groups["msTime"].Value;
                                if (double.TryParse(msTime, out double tryValue))
                                {
                                    startTime = message.Timestamp - TimeSpan.FromMilliseconds(tryValue) + oneMilliSecond;
                                    endTime = message.Timestamp;
                                }
                            }

                            if (startTime > DateTime.MinValue)
                            {
                                var block = new Block()
                                {
                                    StartTime = startTime,
                                    EndTime = endTime,
                                    Text = messageText,
                                    Node = message,
                                };

                                // Add these messages to directly to the lane as to avoid mixing with the Bt+ messages.
                                resultBlocks.Add(block);
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

                if (usingBTTime && blocks.Count > 0 && mttPostStartupTime != DateTime.MinValue)
                {
                    // BT+ timestamp is not a global time, but is relative to the first instance (see QueryPerformanceCounter)
                    // so compute the offset and remove it from all blocks.
                    DateTime offset = blocks.Min(p => p.StartTime);
                    foreach (Block block in blocks)
                    {
                        block.StartTime = mttPostStartupTime + block.StartTime.Subtract(offset);
                        block.EndTime = mttPostStartupTime + block.EndTime.Subtract(offset);
                    }
                }

                resultBlocks.AddRange(blocks);
            }
            else if (node is Microsoft.Build.Logging.StructuredLogger.Task libTask && libTask.Name == "LIB" && libTask.HasChildren)
            {
                bool usingLibTime = globalLibTime;

                foreach (var child in libTask.Children)
                {
                    if (child is Message message)
                    {
                        DateTime endTime = DateTime.MinValue;
                        DateTime startTime = DateTime.MinValue;
                        string messageText = message.Text;

                        var match = libFinalTime.Match(message.Text);
                        if (match.Success)
                        {
                            string filename = match.Groups["filename"].Value;
                            string startTimeValue = match.Groups["startTime"].Value;
                            string endTimeValue = match.Groups["endTime"].Value;
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
                            var block = new Block()
                            {
                                StartTime = startTime,
                                EndTime = endTime,
                                Text = messageText,
                                Node = message,
                            };
                            resultBlocks.Add(block);
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

            return resultBlocks;
        }

        private Block CreateBlock(TimedNode node)
        {
            var block = new Block();
            block.StartTime = node.StartTime;
            block.EndTime = node.EndTime;
            block.Text = node.Name;
            block.Indent = node.GetParentChainIncludingThis().Count();
            block.Node = node;
            return block;
        }
    }
}
