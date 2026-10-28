using Microsoft.Build.Framework;
using Microsoft.Build.Logging.StructuredLogger;
using Xunit;

namespace StructuredLogger.Tests
{
    public class ItemGroupParserTests
    {
        public static string OutputItemsMessagePrefix { get; }
        public static string ItemGroupIncludeMessagePrefix { get; }

        static ItemGroupParserTests()
        {
            lock (typeof(Strings))
            {
                Strings.Initialize("en-US");
                Assert.Equal("en-US", Strings.ResourceSet.Culture);
                Assert.Equal("en-US", Strings.Culture);
                OutputItemsMessagePrefix = Strings.OutputItemsMessagePrefix;
                ItemGroupIncludeMessagePrefix = Strings.ItemGroupIncludeMessagePrefix;
                Assert.NotNull(OutputItemsMessagePrefix);
            }
        }

        [Fact]
        public void AddItemWithMultilineMetadata()
        {
            var result = ItemGroupParser.ParsePropertyOrItemList(@"Added Item(s): 
    Link=
        tmp
                AcceptableNonZeroExitCodes=
                AdditionalDependencies=kernel32.lib;user32.lib;
                ;", OutputItemsMessagePrefix, new StringCache());
        }

        /// <summary>
        /// https://github.com/KirillOsenkov/MSBuildStructuredLog/issues/176
        /// </summary>
        [Fact]
        public void ParseSuggestedBindingRedirectsMetadata()
        {
            var parameter = ItemGroupParser.ParsePropertyOrItemList(@"Output Item(s): 
    SuggestedBindingRedirects=
        Microsoft.Build, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
                MaxVersion=15.1.0.0
        Microsoft.VisualStudio.Validation, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
                MaxVersion=15.3.0.0",
                OutputItemsMessagePrefix, new StringCache()) as Parameter;
            Assert.True(parameter != null);
            Assert.True(parameter.Children.Count == 2);

            var item = parameter.FirstChild as Item;
            Assert.True(item != null);

            var metadata = item.FirstChild as Metadata;
            Assert.True(metadata != null);

            Assert.Equal("SuggestedBindingRedirects", parameter.Name);
            Assert.Equal("Microsoft.Build, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", item.Text);
            Assert.Equal("MaxVersion", metadata.Name);
            Assert.Equal("15.1.0.0", metadata.Value);
        }

        [Fact]
        public void ParseThereWasAConflict()
        {
            var lines = @"""System.IO.Compression, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"" was chosen because it was primary and ""System.IO.Compression, Version=4.1.1.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"" was not.
References which depend on ""System.IO.Compression, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"" [C:\Program Files (x86)\System.IO.Compression.dll].
    C:\Program Files (x86)\System.IO.Compression.dll
      Project file item includes which caused reference ""C:\Program Files (x86)\System.IO.Compression.dll"".
        System.IO.Compression
References which depend on ""System.IO.Compression, Version=4.1.1.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"" [].
    C:\A\\A.dll
      Project file item includes which caused reference ""C:\A\\A.dll"".
        C:\A\bin\Debug\C.dll
    C:\A\\B.dll
      Project file item includes which caused reference ""C:\A\\B.dll"".
        C:\A\\B\D.dll
        C:\A\\C.dll".GetLines();
            var parameter = new Parameter()
            {
                Name = "There was a conflict"
            };
            var stringCache = new StringCache();
            foreach (var line in lines)
            {
                MessageProcessor.HandleThereWasAConflict(parameter, line, stringCache);
            }

            Assert.True(parameter.Children.Count == 3);
            var text = StringWriter.GetString(parameter);
            Assert.Equal(@"There was a conflict
    ""System.IO.Compression, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"" was chosen because it was primary and ""System.IO.Compression, Version=4.1.1.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"" was not.
    References which depend on ""System.IO.Compression, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"" [C:\Program Files (x86)\System.IO.Compression.dll].
        C:\Program Files (x86)\System.IO.Compression.dll
            Project file item includes which caused reference ""C:\Program Files (x86)\System.IO.Compression.dll"".
                System.IO.Compression
    References which depend on ""System.IO.Compression, Version=4.1.1.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"" [].
        C:\A\\A.dll
            Project file item includes which caused reference ""C:\A\\A.dll"".
                C:\A\bin\Debug\C.dll
        C:\A\\B.dll
            Project file item includes which caused reference ""C:\A\\B.dll"".
                C:\A\\B\D.dll
                C:\A\\C.dll
", text);
        }

        /// <summary>
        /// The structured conflict events of binlog format 28 must produce the same tree that
        /// <see cref="ParseThereWasAConflict"/> builds from the legacy message text.
        /// </summary>
        [Fact]
        public void AddStructuredConflictDetails()
        {
            var victor = new AssemblyConflictReferenceDetails(
                "Shared, Version=2.0.0.0",
                @"C:\v2\Shared.dll",
                isPrimary: false,
                isResolved: true,
                unresolvedPrimaryItemSpec: null,
                primarySourceItemSpecs: new string[0],
                dependees: new[] { new AssemblyConflictDependee(@"C:\libs\ClassLibB.dll", new[] { @"C:\libs\ClassLibB.dll" }) });
            var victim = new AssemblyConflictReferenceDetails(
                "Shared, Version=1.0.0.0",
                @"C:\v1\Shared.dll",
                isPrimary: true,
                isResolved: true,
                unresolvedPrimaryItemSpec: null,
                primarySourceItemSpecs: new[] { "Shared, Version=1.0.0.0" },
                dependees: new[] { new AssemblyConflictDependee(@"C:\libs\ClassLibA.dll", new[] { @"C:\libs\ClassLibA.dll" }) });

            var stringCache = new StringCache();
            var parameter = new Parameter { Name = "Conflict" };
            ItemGroupParser.AddConflictHeader(parameter, victor.FusionName, victim.FusionName, AssemblyConflictLossReason.HadLowerVersion, victim.IsPrimary, stringCache);
            ItemGroupParser.AddConflictReferenceDetails(parameter, victor, isVictor: true, stringCache);
            ItemGroupParser.AddConflictReferenceDetails(parameter, victim, isVictor: false, stringCache);

            var text = StringWriter.GetString(parameter).NormalizeLineBreaks();
            var expected = @"Conflict
    There was a conflict between ""Shared, Version=2.0.0.0"" and ""Shared, Version=1.0.0.0"".
    ""Shared, Version=2.0.0.0"" was chosen because it had a higher version.
    References which depend on ""Shared, Version=2.0.0.0"" [C:\v2\Shared.dll].
        C:\libs\ClassLibB.dll
            Project file item includes which caused reference ""C:\libs\ClassLibB.dll"".
                C:\libs\ClassLibB.dll
    References which depend on or have been unified to ""Shared, Version=1.0.0.0"" [C:\v1\Shared.dll].
        C:\v1\Shared.dll
            Project file item includes which caused reference ""C:\v1\Shared.dll"".
                Shared, Version=1.0.0.0
        C:\libs\ClassLibA.dll
            Project file item includes which caused reference ""C:\libs\ClassLibA.dll"".
                C:\libs\ClassLibA.dll
".NormalizeLineBreaks();
            Assert.Equal(expected, text);
        }

        [Fact]
        public void AddStructuredConflictDetailsUnresolvedPrimary()
        {
            var victim = new AssemblyConflictReferenceDetails(
                "System.IO.Compression.FileSystem",
                fullPath: null,
                isPrimary: true,
                isResolved: false,
                unresolvedPrimaryItemSpec: "System.IO.Compression.FileSystem",
                primarySourceItemSpecs: new string[0],
                dependees: new AssemblyConflictDependee[0]);

            var stringCache = new StringCache();
            var parameter = new Parameter { Name = "Conflict" };
            ItemGroupParser.AddConflictHeader(parameter, "System.IO.Compression.FileSystem, Version=4.0.0.0", victim.FusionName, AssemblyConflictLossReason.WasNotPrimary, victim.IsPrimary, stringCache);
            ItemGroupParser.AddConflictReferenceDetails(parameter, victim, isVictor: false, stringCache);

            var text = StringWriter.GetString(parameter).NormalizeLineBreaks();
            var expected = @"Conflict
    There was a conflict between ""System.IO.Compression.FileSystem, Version=4.0.0.0"" and ""System.IO.Compression.FileSystem"".
    ""System.IO.Compression.FileSystem, Version=4.0.0.0"" was chosen because it was primary and ""System.IO.Compression.FileSystem"" was not.
    References which depend on or have been unified to ""System.IO.Compression.FileSystem"" [].
        Unresolved primary reference with an item include of ""System.IO.Compression.FileSystem"".
".NormalizeLineBreaks();
            Assert.Equal(expected, text);
        }

        [Fact]
        public void ParseThereWasAConflictMultiline()
        {
            var message = @"    References which depend on ""System.IO.Compression.FileSystem, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"" [C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\3.1.0\ref\netcoreapp3.1\System.IO.Compression.FileSystem.dll].
        C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\3.1.0\ref\netcoreapp3.1\System.IO.Compression.FileSystem.dll
          Project file item includes which caused reference ""C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\3.1.0\ref\netcoreapp3.1\System.IO.Compression.FileSystem.dll"".
            C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\3.1.0\ref\netcoreapp3.1\System.IO.Compression.FileSystem.dll
    References which depend on ""System.IO.Compression.FileSystem"" [].
        Unresolved primary reference with an item include of ""System.IO.Compression.FileSystem"".".NormalizeLineBreaks();
            var stringCache = new StringCache();
            var parameter = new Parameter();
            ItemGroupParser.ParseThereWasAConflict(parameter, message, stringCache);
            var text = StringWriter.GetString(parameter).NormalizeLineBreaks();
            var expected = @"Parameter
    References which depend on ""System.IO.Compression.FileSystem, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"" [C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\3.1.0\ref\netcoreapp3.1\System.IO.Compression.FileSystem.dll].
        C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\3.1.0\ref\netcoreapp3.1\System.IO.Compression.FileSystem.dll
            Project file item includes which caused reference ""C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\3.1.0\ref\netcoreapp3.1\System.IO.Compression.FileSystem.dll"".
                C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\3.1.0\ref\netcoreapp3.1\System.IO.Compression.FileSystem.dll
    References which depend on ""System.IO.Compression.FileSystem"" [].
        Unresolved primary reference with an item include of ""System.IO.Compression.FileSystem"".
".NormalizeLineBreaks();
            Assert.Equal(expected, text);
        }

        [Fact]
        public void ParseMultilineMetadata()
        {
            var parameter = ItemGroupParser.ParsePropertyOrItemList(@"Added Item(s): 
    _ProjectsFiles=
        Project1
                AdditionalProperties=
        AutoParameterizationWebConfigConnectionStrings=false;
        _PackageTempDir=Out\Dir;
        
        Project2
                AdditionalProperties=
        AutoParameterizationWebConfigConnectionStrings=false;
        _PackageTempDir=Out\Dir;
        
        Project3
                AdditionalProperties=
        AutoParameterizationWebConfigConnectionStrings=false;
        _PackageTempDir=Out\Dir;
        ", ItemGroupIncludeMessagePrefix, new StringCache()) as Parameter;

            //Assert.Equal(3, parameter.Children.Count);
        }
    }
}
