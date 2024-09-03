using Microsoft.Build.Logging.StructuredLogger;
using Xunit;

namespace StructuredLogger.Tests
{
    public class ResourceStringTests
    {
        [Fact]
        public void TestInitialize()
        {
            lock (typeof(Strings))
            {
                var resources = StringsSet.ResourcesCollection;
                var cultures = resources.Keys;

                foreach (var culture in cultures)
                {
                    Strings.Initialize(culture);
                    Assert.Equal(culture, Strings.ResourceSet.Culture);
                    Assert.NotNull(Strings.OutputItemsMessagePrefix);
                }
            }
        }
    }
}
