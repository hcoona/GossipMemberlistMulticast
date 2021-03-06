using System;
using System.Collections.Generic;

namespace GossipMemberlistMulticast
{
    internal static class ListExtensions
    {
        public static T ChooseRandom<T>(this IReadOnlyList<T> source, Random random)
        {
            var index = random.Next(source.Count);
            return source[index];
        }
    }
}
