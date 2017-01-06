using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot
{
    public class MathHelpers
    {
        private static Random _rand;

        public static Random Rnd => _rand ?? (_rand = new Random());

        /// <summary>
        /// Returns an array of random integers values. Each value will be different.
        /// </summary>
        /// <param name="count">The number of indicies to produce.</param>
        /// <param name="maxIndex">The max index that can be produced.</param>
        /// <returns></returns>
        public static int[] GetRandomIndicies(int count, int maxIndex)
        {
            if (maxIndex < count) throw new Exception("Max index must be the same or greater then count!");

            return Enumerable.Range(0, maxIndex).Select(i => new Tuple<int, int>(Rnd.Next(maxIndex), i))
                .OrderBy(i => i.Item1)
                .Select(i => i.Item2)
                .Take(count).ToArray();
        }

        /// <summary>
        /// Returns a random number from 0 to the max index.
        /// </summary>
        /// <param name="maxIndex">The max index.</param>
        /// <returns></returns>
        public static int GenerateRandomNumber(int maxIndex)
        {
            int[] indicies = GetRandomIndicies(maxIndex, maxIndex);
            return indicies[Rnd.Next(indicies.Length)];
        }
    }
}
