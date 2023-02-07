using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

// TODO: remove whole file once AssemblyLoadBuildEventArgs are imported via Microsoft.Build.Framework package

namespace Microsoft.Build.Framework
{
    internal enum AssemblyLoadingContext
    {
        TaskRun,
        Evaluation,
        SdkResolution,
        LoggerInitialization,
    }

    internal sealed class AssemblyLoadBuildEventArgs : BuildMessageEventArgs
    {
        private const string DefaultAppDomainDescriptor = "[Default]";

        public AssemblyLoadBuildEventArgs()
        { }

        public AssemblyLoadBuildEventArgs(
            AssemblyLoadingContext loadingContext,
            string? loadingInitiator,
            string? assemblyName,
            string assemblyPath,
            Guid mvid,
            string? customAppDomainDescriptor,
            MessageImportance importance = MessageImportance.Low)
            : base(null, null, null, importance, DateTime.UtcNow, assemblyName, assemblyPath, mvid)
        {
            LoadingContext = loadingContext;
            LoadingInitiator = loadingInitiator;
            AssemblyName = assemblyName;
            AssemblyPath = assemblyPath;
            MVID = mvid;
            AppDomainDescriptor = customAppDomainDescriptor;
        }

        public AssemblyLoadingContext LoadingContext { get; private set; }
        public string? LoadingInitiator { get; private set; }
        public string? AssemblyName { get; private set; }
        public string? AssemblyPath { get; private set; }
        public Guid MVID { get; private set; }
        // Null string indicates that load occurred on Default AppDomain (for both Core and Framework).
        public string? AppDomainDescriptor { get; private set; }

        public override string Message
        {
            get
            {
                if (RawMessage == null)
                {
                    string? loadingInitiator = LoadingInitiator == null ? null : $" ({LoadingInitiator})";
                    RawMessage = string.Format("Assembly loaded during {0}{1}: {2} (location: {3}, MVID: {4}, AppDomain: {5})", LoadingContext.ToString(), loadingInitiator, AssemblyName, AssemblyPath, MVID.ToString(), AppDomainDescriptor ?? DefaultAppDomainDescriptor);
                }

                return RawMessage;
            }
        }
    }
}
