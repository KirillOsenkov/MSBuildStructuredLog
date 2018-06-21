using Microsoft.Build.Logging.StructuredLogger;
using Xunit;

namespace StructuredLogger.Tests
{
    public class ItemGroupParserTests
    {
        [Fact]
        public void AddItemWithMultilineMetadata()
        {
            var result = ItemGroupParser.ParsePropertyOrItemList(@"Added Item(s): 
    Link=
        tmp
                AcceptableNonZeroExitCodes=
                AdditionalDependencies=kernel32.lib;user32.lib;
                ;", MessageProcessor.OutputItemsMessagePrefix, new StringCache());
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
                MessageProcessor.OutputItemsMessagePrefix, new StringCache()) as Parameter;
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
    }
}
