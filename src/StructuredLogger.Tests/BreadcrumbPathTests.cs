using System;
using Microsoft.Build.Logging.StructuredLogger;
using Xunit;
using Task = Microsoft.Build.Logging.StructuredLogger.Task;

namespace StructuredLogger.Tests
{
    public class BreadcrumbPathTests
    {
        private static Property AddPath(Build build, string configuration)
        {
            var project = new Project
            {
                Name = "app (" + configuration + ")",
                ProjectFile = @"C:\repo\app.csproj",
                Configuration = configuration,
                Id = configuration == "Debug" ? 12 : 99,
                StartTime = DateTime.Now
            };
            var target = new Target { Name = "Compile", Id = project.Id + 1 };
            var task = new Task { Name = "Csc", Id = project.Id + 2 };
            var property = new Property { Name = "DefineConstants", Value = configuration };
            build.AddChild(project);
            project.AddChild(target);
            target.AddChild(task);
            task.AddChild(property);
            return property;
        }

        [Fact]
        public void NavigatesAcrossBuildNamesConfigurationsIdsAndPropertyValues()
        {
            var source = new Build { Name = "debug.binlog" };
            var destination = new Build { Name = "release.binlog" };
            var sourceNode = AddPath(source, "Debug");
            var destinationNode = AddPath(destination, "Release");
            var path = BreadcrumbPath.Create(sourceNode);

            Assert.Contains(@"Project: C:\repo\app.csproj", path);
            Assert.Contains("Property: DefineConstants", path);
            Assert.DoesNotContain("debug.binlog", path);
            Assert.DoesNotContain("Debug", path);
            Assert.Equal(BreadcrumbPathResult.Found, BreadcrumbPath.Resolve(destination, path, out var match));
            Assert.Same(destinationNode, match);
        }

        [Theory]
        [InlineData("name: with > / separators")]
        [InlineData("name\r\nwith\nnewlines\rand\0null")]
        [InlineData("literal %0A %25 %")]
        [InlineData("  leading and trailing whitespace  ")]
        public void RoundTripsNamesWithoutTreatingTheirContentsAsPathSyntax(string name)
        {
            var build = new Build();
            var child = new Folder { Name = name };
            build.AddChild(child);

            var path = BreadcrumbPath.Create(child);
            Assert.Equal(BreadcrumbPathResult.Found, BreadcrumbPath.Resolve(build, path, out var match));
            Assert.Same(child, match);
        }

        [Fact]
        public void ProjectPathsMatchAcrossSlashStylesAndCasing()
        {
            var source = new Build();
            var destination = new Build();
            var sourceNode = AddPath(source, "Debug");
            var destinationNode = AddPath(destination, "Release");
            destination.FindFirstDescendant<Project>().ProjectFile = "c:/REPO/APP.csproj";

            Assert.Equal(BreadcrumbPathResult.Found, BreadcrumbPath.Resolve(destination, BreadcrumbPath.Create(sourceNode), out var match));
            Assert.Same(destinationNode, match);
        }

        [Fact]
        public void EvaluationPathsIgnoreRuntimeIdsAndDisplayNames()
        {
            var source = new Build();
            var destination = new Build();
            var sourceNode = new ProjectEvaluation { ProjectFile = @"C:\repo\app.csproj", Name = "Debug evaluation", Id = 12 };
            var destinationNode = new ProjectEvaluation { ProjectFile = "c:/REPO/APP.csproj", Name = "Release evaluation", Id = 99 };
            source.AddChild(sourceNode);
            destination.AddChild(destinationNode);

            Assert.Equal(BreadcrumbPathResult.Found, BreadcrumbPath.Resolve(destination, BreadcrumbPath.Create(sourceNode), out var match));
            Assert.Same(destinationNode, match);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void ProjectsWithoutFilePathsUseTheirNames(string projectFile)
        {
            var source = new Build();
            var destination = new Build();
            var sourceNode = new Project { Name = "app", ProjectFile = projectFile };
            var destinationNode = new Project { Name = "app", ProjectFile = projectFile };
            source.AddChild(sourceNode);
            destination.AddChild(new Project { Name = "other", ProjectFile = projectFile });
            destination.AddChild(destinationNode);

            Assert.Equal(BreadcrumbPathResult.Found, BreadcrumbPath.Resolve(destination, BreadcrumbPath.Create(sourceNode), out var match));
            Assert.Same(destinationNode, match);
        }

        [Fact]
        public void DoesNotChooseAnArbitraryDuplicate()
        {
            var build = new Build();
            var first = AddPath(build, "Debug");
            AddPath(build, "Release");

            Assert.Equal(BreadcrumbPathResult.Ambiguous, BreadcrumbPath.Resolve(build, BreadcrumbPath.Create(first), out var match));
            Assert.Null(match);
        }

        [Fact]
        public void LaterSegmentsCanDisambiguateDuplicateParents()
        {
            var source = new Build();
            var sourceNode = AddPath(source, "Debug");
            var destination = new Build();
            destination.AddChild(new Project { ProjectFile = @"C:\repo\app.csproj" });
            destination.AddChild(new Project { ProjectFile = @"C:\repo\app.csproj" });
            var destinationNode = AddPath(destination, "Release");

            Assert.Equal(BreadcrumbPathResult.Found, BreadcrumbPath.Resolve(destination, BreadcrumbPath.Create(sourceNode), out var match));
            Assert.Same(destinationNode, match);
        }

        [Fact]
        public void RequiresTheEntireParentChainAndTheNodeType()
        {
            var source = new Build();
            var sourceNode = AddPath(source, "Debug");
            var destination = new Build();
            var destinationNode = AddPath(destination, "Release");
            destinationNode.Name = "DifferentProperty";

            Assert.Equal(BreadcrumbPathResult.NotFound, BreadcrumbPath.Resolve(destination, BreadcrumbPath.Create(sourceNode), out var match));
            Assert.Null(match);

            var task = destinationNode.Parent;
            task.AddChild(new Item { Text = "DefineConstants" });
            Assert.Equal(BreadcrumbPathResult.NotFound, BreadcrumbPath.Resolve(destination, BreadcrumbPath.Create(sourceNode), out match));
            Assert.Null(match);
        }

        [Fact]
        public void RootPathNavigatesToTheOtherBuild()
        {
            var source = new Build { Name = "one.binlog" };
            var destination = new Build { Name = "two.binlog" };
            Assert.Equal(BreadcrumbPathResult.Found, BreadcrumbPath.Resolve(destination, BreadcrumbPath.Create(source) + "\r\n", out var match));
            Assert.Same(destination, match);
            Assert.Null(BreadcrumbPath.Create(new Folder { Name = "detached" }));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("random clipboard text")]
        [InlineData("MSBuild path v2")]
        [InlineData("MSBuild path v1\nMissing separator")]
        [InlineData("MSBuild path v1\n: empty type")]
        [InlineData("MSBuild path v1\nFolder: bad%")]
        [InlineData("MSBuild path v1\nFolder: bad%FF")]
        public void InvalidClipboardTextDoesNotNavigate(string path)
        {
            Assert.Equal(BreadcrumbPathResult.Invalid, BreadcrumbPath.Resolve(new Build(), path, out var match));
            Assert.Null(match);
        }
    }
}
