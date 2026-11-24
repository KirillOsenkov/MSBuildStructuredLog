using System;
using System.Collections.Generic;
using Microsoft.Build.Logging.StructuredLogger;
using Xunit;

namespace StructuredLogger.Tests
{
    public class ReflectorTests
    {
        [Fact]
        public void SelectsPublicCallbackOverloadOfEnumerateItemsPerType()
        {
            AssertCallbackOverload(typeof(CurrentItemDictionary<string>));
        }

        [Fact]
        public void SelectsNonPublicCallbackOverloadOfEnumerateItemsPerType()
        {
            AssertCallbackOverload(typeof(LegacyItemDictionary<string>));
        }

        private static void AssertCallbackOverload(Type itemDictionaryType)
        {
            var method = Reflector.FindEnumerateItemsPerTypeMethod(itemDictionaryType);
            Assert.NotNull(method);
            Assert.Equal(typeof(void), method.ReturnType);
            var parameter = Assert.Single(method.GetParameters());
            Assert.Equal(typeof(Action<,>), parameter.ParameterType.GetGenericTypeDefinition());
        }

        private sealed class CurrentItemDictionary<T>
        {
            public IEnumerable<(string itemType, IEnumerable<T> itemValue)> EnumerateItemsPerType()
            {
                throw new NotSupportedException();
            }

            public void EnumerateItemsPerType(Action<string, IEnumerable<T>> callback)
            {
            }
        }

        private sealed class LegacyItemDictionary<T>
        {
            private void EnumerateItemsPerType(Action<string, IEnumerable<T>> callback)
            {
            }
        }
    }
}
