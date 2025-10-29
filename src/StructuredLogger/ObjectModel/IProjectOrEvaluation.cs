﻿using System;
using System.Collections.Generic;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public interface IProjectOrEvaluation
    {
        string ProjectFile { get; set; }
        string TargetFramework { get; set; }
        bool IsOuterProject { get; set; }
        string Platform { get; set; }
        string Configuration { get; set; }
    }

    public static class ProjectOrEvaluationHelper
    {
        public static ProjectEvaluation GetEvaluation(this IProjectOrEvaluation projectOrEvaluation, Build build = null)
        {
            if (projectOrEvaluation is ProjectEvaluation evaluation)
            {
                return evaluation;
            }

            if (projectOrEvaluation is Project project)
            {
                evaluation = null;

                if (build == null)
                {
                    build = project.GetRoot() as Build;
                }

                if (build != null)
                {
                    evaluation = build.FindEvaluation(project.EvaluationId);
                }

                return evaluation;
            }

            return null;
        }

        private static (string, bool, string, string) GetKey(IProjectOrEvaluation p)
        {
            return (p.TargetFramework, p.IsOuterProject, p.Configuration, p.Platform);
        }

        private const string separator = ",";

        public static void ClearCache()
        {
            AdornmentStringCache.Clear();
        }

        private static Dictionary<(string, bool, string, string), string> AdornmentStringCache = new();

        public static bool ShowConfigurationAndPlatform;

        public static string GetAdornmentString(this IProjectOrEvaluation project)
        {
            var key = GetKey(project);

            if (!AdornmentStringCache.TryGetValue(key, out string value))
            {
                value = CreateAdornment(project);
                AdornmentStringCache[key] = value;
            }

            return value;
        }

        [ThreadStatic]
        private static List<string> strings;

        private static string CreateAdornment(IProjectOrEvaluation project)
        {
            if (strings == null)
            {
                strings = new List<string>(3);
            }

            if (project.TargetFramework is { Length: > 0 } targetFramework)
            {
                if (project.IsOuterProject && !targetFramework.Contains(";"))
                {
                    targetFramework = "TargetFrameworks: " + targetFramework;
                }

                strings.Add(targetFramework);
            }

            if (ShowConfigurationAndPlatform)
            {
                if (project.Configuration is { Length: > 0 } configuration)
                {
                    strings.Add(configuration);
                }

                if (project.Platform is { Length: > 0 } platform)
                {
                    strings.Add(platform);
                }
            }

            var result = string.Join(separator, strings);

            strings.Clear();

            return result;
        }
    }
}
