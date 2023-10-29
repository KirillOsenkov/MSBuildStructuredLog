using System;
using System.Collections.Generic;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public static class Utilities
    {
        public static int BinarySearch<T, C>(this IList<T> list, C item, Func<T, C> comparableSelector)
            where C : IComparable<C>
        {
            int count = list.Count;
            int lo = 0;
            int hi = count - 1;

            while (lo <= hi)
            {
                int i = lo + ((hi - lo) >> 1);
                var comparable = comparableSelector(list[i]);
                int order = comparable.CompareTo(item);

                if (order == 0)
                {
                    return i;
                }

                if (order < 0)
                {
                    lo = i + 1;
                }
                else
                {
                    hi = i - 1;
                }
            }

            return ~lo;
        }
    }
}