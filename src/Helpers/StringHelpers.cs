using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DiscordBot
{
    public class StringHelpers
    {
        /// <summary>
        /// Automatically captilizes sentences. Also fixes captials for self referencing.
        /// </summary>
        /// <param name="input">THe string to captilize.</param>
        /// <param name="sentenceStart">Whether this is the start of a sentence.</param>
        /// <returns></returns>
        public static string AutoCapitals(string input, bool sentenceStart)
        {
            char startChar = sentenceStart ? char.ToUpper(input[0]) : char.ToLower(input[0]);
            input = input.Remove(0, 1).Insert(0, startChar.ToString());
            input = input.Replace(" i ", " I ");
            input = input.Replace("i'm", "I'm");

            return input;
        }

        /// <summary>
        /// Returns the absolute path to a sound.
        /// </summary>
        /// <param name="soundName">The name of the sound.</param>
        /// <returns></returns>
        public static string GetSoundLocation(string soundName)
        {
            return $"{Environment.CurrentDirectory}/sounds/{soundName.Replace(".wav", "")}.wav";
        }

        /// <summary>
        /// Finds all instances of a substring in a string.
        /// </summary>
        /// <param name="str"></param>
        /// <param name="substr"></param>
        /// <param name="ignoreCase"></param>
        /// <returns></returns>
        public static int[] AllIndexesOf(string str, string substr, bool ignoreCase = false)
        {
            if (string.IsNullOrWhiteSpace(str) ||
                string.IsNullOrWhiteSpace(substr))
            {
                throw new ArgumentException("String or substring is not specified.");
            }

            var indexes = new List<int>();
            int index = 0;

            while ((index = str.IndexOf(substr, index, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) != -1)
            {
                indexes.Add(index++);
            }

            return indexes.ToArray();
        }
    }
}
