using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Build.Framework;

namespace TaskRunner
{
    public class BuildEngine : IBuildEngine4
    {
        public int ColumnNumberOfTaskNode => 0;

        public bool ContinueOnError => true;

        public int LineNumberOfTaskNode => 0;

        public string ProjectFileOfTaskNode => null;

        public bool IsRunningMultipleNodes => false;

        public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs)
        {
            throw new NotImplementedException(nameof(BuildProjectFile));
        }

        public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs, string toolsVersion)
        {
            throw new NotImplementedException(nameof(BuildProjectFile));
        }

        public BuildEngineResult BuildProjectFilesInParallel(string[] projectFileNames, string[] targetNames, IDictionary[] globalProperties, IList<string>[] removeGlobalProperties, string[] toolsVersion, bool returnTargetOutputs)
        {
            throw new NotImplementedException(nameof(BuildProjectFilesInParallel));
        }

        public bool BuildProjectFilesInParallel(string[] projectFileNames, string[] targetNames, IDictionary[] globalProperties, IDictionary[] targetOutputsPerProject, string[] toolsVersion, bool useResultsCache, bool unloadProjectsOnCompletion)
        {
            throw new NotImplementedException(nameof(BuildProjectFilesInParallel));
        }

        public void LogCustomEvent(CustomBuildEventArgs e)
        {
            Console.WriteLine(e.Message);
        }

        public void LogErrorEvent(BuildErrorEventArgs e)
        {
            Console.WriteLine(e.Message);
        }

        public void LogMessageEvent(BuildMessageEventArgs e)
        {
            Console.WriteLine(e.Message);
        }

        public void LogWarningEvent(BuildWarningEventArgs e)
        {
            Console.WriteLine(e.Message);
        }

        public void Reacquire()
        {
            throw new NotImplementedException(nameof(Reacquire));
        }

        public void Yield()
        {
            throw new NotImplementedException(nameof(Yield));
        }

        private Dictionary<object, object> registeredTaskObjects = new Dictionary<object, object>();

        public void RegisterTaskObject(object key, object obj, RegisteredTaskObjectLifetime lifetime, bool allowEarlyCollection)
        {
            registeredTaskObjects[key] = obj;
        }

        public object GetRegisteredTaskObject(object key, RegisteredTaskObjectLifetime lifetime)
        {
            registeredTaskObjects.TryGetValue(key, out var value);
            return value;
        }

        public object UnregisterTaskObject(object key, RegisteredTaskObjectLifetime lifetime)
        {
            registeredTaskObjects.TryGetValue(key, out var value);
            registeredTaskObjects.Remove(key);
            return value;
        }
    }
}
