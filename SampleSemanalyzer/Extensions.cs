using System;
using System.Collections.Generic;

namespace SampleSemanalyzer
{
    public static class Extensions
    {
        public static void ForEach<T>(this IEnumerable<T> e, Action<T> predicate)
        {
            foreach (var x in e)
            {
                predicate(x);
            }
        }
    }
}