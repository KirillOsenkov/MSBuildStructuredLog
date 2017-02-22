using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.Serialization;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging.Serialization;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class LogReplayEventSource : EventArgsDispatcher
    {
        private Action<BuildEventArgs, BinaryReader, int> CreateFromStream = (Action<BuildEventArgs, BinaryReader, int>)Delegate.CreateDelegate(
                typeof(Action<BuildEventArgs, BinaryReader, int>),
                typeof(BuildEventArgs).GetMethod("CreateFromStream", BindingFlags.Instance | BindingFlags.NonPublic));

        private static readonly Assembly frameworkAssembly = Assembly.Load(AssemblyName.GetAssemblyName(@"C:\MSBuild\bin\Bootstrap\15.0\Bin\Microsoft.Build.Framework.dll"));

        private Type GetType(int metadataToken, Dictionary<int, Type> typeLookup)
        {
            Type result;
            if (typeLookup.TryGetValue(metadataToken, out result))
            {
                return result;
            }

            result = frameworkAssembly.ManifestModule.ResolveType(metadataToken);
            typeLookup[metadataToken] = result;
            return result;
        }

        public void Replay(string sourceFilePath)
        {
            Dictionary<int, Type> typeLookup = new Dictionary<int, Type>();

            using (var stream = new FileStream(sourceFilePath, FileMode.Open))
            {
                var gzipStream = new GZipStream(stream, CompressionMode.Decompress, leaveOpen: true);
                var binaryReader = new BetterBinaryReader(gzipStream);
                EventArgsReader reader = new EventArgsReader(binaryReader);
                while (true)
                {
                    var instance = reader.Read();
                    if (instance == null)
                    {
                        break;
                    }
                    //int metadataToken = binaryReader.ReadInt32();
                    //Type type = GetType(metadataToken, typeLookup);
                    //type = typeof(BuildEventArgs).Assembly.GetType(type.FullName);

                    //var instance = (BuildEventArgs)FormatterServices.GetUninitializedObject(type);
                    //CallOnDeserialized(instance);

                    //CreateFromStream(instance, binaryReader, 21);

                    Dispatch(instance);
                }
            }
        }

        private Action<LazyFormattedBuildEventArgs, StreamingContext> onDeserialized;

        private void CallOnDeserialized(BuildEventArgs instance)
        {
            if (instance is LazyFormattedBuildEventArgs)
            {
                if (onDeserialized == null)
                {
                    var type = typeof(LazyFormattedBuildEventArgs);
                    MethodInfo method;
                    method = type.GetMethod("OnDeserialized", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (method != null)
                    {
                        onDeserialized = (Action<LazyFormattedBuildEventArgs, StreamingContext>)Delegate.CreateDelegate(
                            typeof(Action<LazyFormattedBuildEventArgs, StreamingContext>),
                            method);
                    }
                }

                onDeserialized((LazyFormattedBuildEventArgs)instance, default(StreamingContext));
            }
        }
    }
}
