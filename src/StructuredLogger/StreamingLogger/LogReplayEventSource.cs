using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class LogReplayEventSource : EventArgsDispatcher
    {
        private Action<BuildEventArgs, BinaryReader, int> CreateFromStream = (Action<BuildEventArgs, BinaryReader, int>)Delegate.CreateDelegate(
                typeof(Action<BuildEventArgs, BinaryReader, int>),
                typeof(BuildEventArgs).GetMethod("CreateFromStream", BindingFlags.Instance | BindingFlags.NonPublic));

        public LogReplayEventSource(string logFilePath)
        {
            FilePath = logFilePath;
        }

        private static readonly Assembly frameworkAssembly = Assembly.Load(AssemblyName.GetAssemblyName(@"C:\MSBuild\bin\Bootstrap\15.0\Bin\Microsoft.Build.Framework.dll"));

        public string FilePath { get; private set; }

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

        public void Replay()
        {
            Dictionary<int, Type> typeLookup = new Dictionary<int, Type>();

            using (var stream = new FileStream(FilePath, FileMode.Open))
            {
                var binaryReader = new BetterBinaryReader(stream);
                while (stream.Position < stream.Length)
                {
                    int metadataToken = binaryReader.ReadInt32();
                    Type type = GetType(metadataToken, typeLookup);
                    type = typeof(BuildEventArgs).Assembly.GetType(type.FullName);

                    var instance = (BuildEventArgs)FormatterServices.GetUninitializedObject(type);
                    CallOnDeserialized(instance);

                    CreateFromStream(instance, binaryReader, 21);

                    Dispatch(instance);
                }
            }
        }

        private Dictionary<Type, Action<LazyFormattedBuildEventArgs, StreamingContext>> onDeserializedMap = new Dictionary<Type, Action<LazyFormattedBuildEventArgs, StreamingContext>>();

        private void CallOnDeserialized(BuildEventArgs instance)
        {
            var type = instance.GetType();
            MethodInfo method;
            Action<LazyFormattedBuildEventArgs, StreamingContext> result;
            if (!onDeserializedMap.TryGetValue(type, out result))
            {
                var baseType = type;
                while (baseType != null)
                {
                    method = baseType.GetMethod("OnDeserialized", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (method != null)
                    {
                        result = (Action<LazyFormattedBuildEventArgs, StreamingContext>)Delegate.CreateDelegate(
                            typeof(Action<LazyFormattedBuildEventArgs, StreamingContext>), 
                            method);
                        onDeserializedMap[type] = result;
                        result((LazyFormattedBuildEventArgs)instance, default(StreamingContext));
                        return;
                    }

                    baseType = baseType.BaseType;
                }
            }
            else
            {
                result((LazyFormattedBuildEventArgs)instance, default(StreamingContext));
            }
        }
    }
}
