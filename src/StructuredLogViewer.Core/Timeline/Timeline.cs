using System;
using System.Collections.Generic;
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
            string regexStartupPhase = @"^CL\.exe will run on (?'activeFiles'[0-9]*) out of (?'totalFiles'[0-9]*) file\(s\)\.\s*Startup phase took (?'timeMS'[0-9]*.[0-9]*)ms.$";
            Regex startupPhase = new Regex(regexStartupPhase, RegexOptions.Multiline);
            string regexCleanupPhase = @"^Cleanup phase took (?'timeMS'[0-9]*.[0-9]*)ms.$";
            Regex cleanupPhase = new Regex(regexCleanupPhase, RegexOptions.Multiline);
            string regexTaskTime = @"^Task '(?'filename'.*)' took (?'msTime'([0-9]*\.[0-9]+|[0-9]+))ms.$";
            Regex TaskTime = new Regex(regexTaskTime, RegexOptions.Multiline);

            build.VisitAllChildren<TimedNode>(node =>
            {
                if (node is Build)
                {
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
                if (includeCpp && node is Microsoft.Build.Logging.StructuredLogger.Task task2 && node.Name == "MultiToolTask" && task2.HasChildren)
                {
                    TimeSpan oneMS = TimeSpan.FromMilliseconds(1);
                    // parse for:
                    // midl.exe will run on 8 out of 8 file(s) in 8 batches.  Startup phase took 46.0028ms.
                    // Task 'CodeStore.idl' took 2244ms.
                    // Cleanup phase took 68ms.
                    foreach (var child in task2.Children)
                    {
                        if (child is Message message)
                        {
                            TimeSpan timeDuration = TimeSpan.Zero;
                            string messageText = string.Empty;
                            var match = TaskTime.Match(message.Text);
                            if (match.Success)
                            {
                                var filename = match.Groups["filename"].Value;
                                var msTime = match.Groups["msTime"].Value;
                                if (double.TryParse(msTime, out double tryValue) && !string.IsNullOrWhiteSpace(filename))
                                {
                                    timeDuration = TimeSpan.FromMilliseconds(tryValue);
                                    messageText = filename;
                                }
                            }
                            else
                            {
                                match = startupPhase.Match(message.Text);
                                if (match.Success)
                                {
                                    var msTime = match.Groups["msTime"].Value;
                                    if (double.TryParse(msTime, out double tryValue))
                                    {
                                        timeDuration = TimeSpan.FromMilliseconds(tryValue);
                                    }
                                }
                                else
                                {
                                    match = startupPhase.Match(message.Text);
                                    var msTime = match.Groups["msTime"].Value;
                                    if (double.TryParse(msTime, out double tryValue))
                                    {
                                        timeDuration = TimeSpan.FromMilliseconds(tryValue);
                                    }
                                }
                            }

                            if (timeDuration > TimeSpan.Zero)
                            {
                                var blockChild = new Block();
                                // MTT messages only print duration, assume that the message printed is the end time.
                                // Also trim 1ms from duration so that the graph fits better.
                                blockChild.StartTime = message.Timestamp - timeDuration + oneMS;
                                blockChild.EndTime = message.Timestamp;
                                blockChild.Text = string.IsNullOrEmpty(messageText) ? message.Text : messageText;
                                blockChild.Indent = message.GetParentChainIncludingThis().Count();
                                blockChild.Node = message;
                                lane.Add(blockChild);
                            }
                        }
                    }
                }

                var block = CreateBlock(node);
                lane.Add(block);
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
