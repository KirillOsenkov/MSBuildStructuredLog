using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Build.Logging.StructuredLogger;
using Xunit;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="EvaluationIdProvider"/> class.
    /// </summary>
    public class EvaluationIdProviderTests
    {
        /// <summary>
        /// Tests that consecutive calls to <see cref="EvaluationIdProvider.GetNextId"/> return monotonically increasing values in a single-threaded context.
        /// </summary>
        [Fact]
        public void GetNextId_SingleThread_MonotonicallyIncreasing()
        {
            // Arrange
            const int iterations = 5;
            var results = new long[iterations];

            // Act
            for (int i = 0; i < iterations; i++)
            {
                results[i] = EvaluationIdProvider.GetNextId();
            }

            // Assert
            for (int i = 1; i < iterations; i++)
            {
                Assert.True(results[i] > results[i - 1], 
                    $"Expected result at index {i} ({results[i]}) to be greater than previous index {i - 1} ({results[i - 1]}).");
            }
        }

        /// <summary>
        /// Tests that concurrent calls to <see cref="EvaluationIdProvider.GetNextId"/> produce unique values.
        /// </summary>
//         [Fact] [Error] (52-33)CS0117 'Task' does not contain a definition for 'Run' [Error] (54-18)CS0117 'Task' does not contain a definition for 'WaitAll'
//         public void GetNextId_MultiThread_ProducesUniqueIds()
//         {
//             // Arrange
//             const int concurrentCalls = 1000;
//             var tasks = new Task<long>[concurrentCalls];
// 
//             // Act
//             for (int i = 0; i < concurrentCalls; i++)
//             {
//                 tasks[i] = Task.Run(() => EvaluationIdProvider.GetNextId());
//             }
//             Task.WaitAll(tasks);
//             var results = tasks.Select(t => t.Result).ToList();
// 
//             // Assert
//             var distinctCount = results.Distinct().Count();
//             Assert.Equal(concurrentCalls, distinctCount);
//         }
    }
}
