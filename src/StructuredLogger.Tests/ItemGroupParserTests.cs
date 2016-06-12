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
    }
}
