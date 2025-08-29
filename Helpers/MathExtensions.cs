using System;
using System.Collections.Generic;
using System.Linq;

namespace Koffiemachine.Helpers
{
    public static class MathExtensions
    {
        public static double StdDev(this IEnumerable<decimal> values)
        {
            var doubleValues = values.Select(v => (double)v).ToList();
            if (doubleValues.Count <= 1) return 0.0;

            double avg = doubleValues.Average();
            double sumSq = doubleValues.Sum(d => Math.Pow(d - avg, 2));
            return Math.Sqrt(sumSq / (doubleValues.Count - 1));
        }
    }
}
