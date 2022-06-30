using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogViewer
{
    public class Timeline
    {
        public Dictionary<int, Lane> Lanes { get; set; } = new Dictionary<int, Lane>();

        public Timeline(Build build, bool includeCpp)
        {
            Populate(build, includeCpp);
        }

        private void Populate(Build build, bool includeCpp = false)
        {
            // parse for:
            // midl.exe will run on 8 out of 8 file(s) in 8 batches.  Startup phase took 46.0028ms.
            // Task 'CodeStore.idl' took 2244ms.
            // Cleanup phase took 68ms.
            string regexStartupPhase = @"^CL\.exe will run on (?'activeFiles'[0-9]*) out of (?'totalFiles'[0-9]*) file\(s\) in [0-9]* batches\.\s*Startup phase took (?'msTime'[0-9]*.[0-9]*)ms.$";
            Regex startupPhase = new Regex(regexStartupPhase, RegexOptions.Multiline);
            string regexCleanupPhase = @"^Cleanup phase took (?'msTime'[0-9]*.[0-9]*)ms.$";
            Regex cleanupPhase = new Regex(regexCleanupPhase, RegexOptions.Multiline);
            string regexTaskTime = @"^Task '(?'filename'.*)' took (?'msTime'([0-9]*\.[0-9]+|[0-9]+))ms.$";
            Regex TaskTime = new Regex(regexTaskTime, RegexOptions.Multiline);

            // time(C:\Program Files (x86)\Microsoft Visual Studio\2019\Preview\VC\Tools\MSVC\14.24.28218\bin\Hostx86\x86\c1xx.dll)=0.83512s < 985096605139 - 985104956295 > BB [C:\Users\yuehuang\AppData\Local\Temp\123\main36.cpp]
            // time(C:\Program Files(x86)\Microsoft Visual Studio\2019\Preview\VC\Tools\MSVC\14.24.28218\bin\Hostx86\x86\c2.dll)=0.01935s < 985104875296 - 985105068765 > BB[C: \Users\yuehuang\AppData\Local\Temp\123\main47.cpp]
            string regexBTPlus = @"^time\(.*(c1xx\.dll|c2\.dll)\)=(?'msTime'([0-9]*\.[0-9]+|[0-9]+))s \< (?'startTime'[\d]*) - (?'endTime'[\d]*) \>\s*BB\s*\[(?'filename'[^\]]*)\]$";
            Regex BTPlus = new Regex(regexBTPlus, RegexOptions.Multiline);
            bool globalBtplus = false;

            build.VisitAllChildren<TimedNode>(node =>
            {
                if (node is Build)
                {
                    if (includeCpp)
                    {
                        // Search for Global Bt+
                        node.FindFirstChild<Folder>(childNode =>
                        {
                            if (childNode.Name == "Environment")
                            {
                                childNode.FindFirstChild<Property>(envProperty =>
                                {
                                    if (envProperty.Name == "_CL_" || envProperty.Name == "__CL__")
                                    {
                                        globalBtplus = envProperty.Value.Contains("/Bt+");
                                        return globalBtplus;
                                    }
                                    return false;
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

                // MultiToolTask batches tasks and runs them in parallel.
                // For this view, de-batch them into individual task units.
                if (includeCpp && node is Microsoft.Build.Logging.StructuredLogger.Task cppTask && (node.Name == "MultiToolTask" || node.Name == "CL") && cppTask.HasChildren)
                {
                    TimeSpan oneMilliSecond = TimeSpan.FromMilliseconds(1);
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
                            else
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

                                    // Add these messages to directly to the lane so to avoid mixing with the Bt+ messages.
                                    lane.Add(block);
                                }
                            }
                        }

                        if (!usingBTTime && child is Property property)
                        {
                            if (property.Name == "CommandLineArgument" && property.Value.Contains("/Bt+"))
                            {
                                usingBTTime = true;
                            }
                        }
                    }

                    if (usingBTTime && blocks.Count > 0)
                    {
                        // BT+ timestamp is not a global time, but is relative to the first instance,
                        // so compute the offset and remove it the task offset.
                        DateTime offset = blocks.Min(p => p.StartTime);
                        foreach (Block block in blocks)
                        {
                            block.StartTime = mttPostStartupTime + block.StartTime.Subtract(offset);
                            block.EndTime = mttPostStartupTime + block.EndTime.Subtract(offset);
                        }
                    }

                    lane.AddRange(blocks);
                }

                lane.Add(CreateBlock(node));
            });
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
