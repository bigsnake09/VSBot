using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot
{
    /// <summary>
    /// Represents a sentence.
    /// </summary>
    public class Sentence
    {
        /// <summary>
        /// The string that presents this sentence.
        /// </summary>
        public string StrSentence;
        /// <summary>
        /// The connectors that can be added to the end of this sentence.
        /// </summary>
        public string[] Connectors;
        /// <summary>
        /// The type of sentence that this is.
        /// </summary>
        public SentenceType Type;

        public string GetRandomConnector()
        {
            if (Connectors == null) return "";
            if (Connectors.Length == 0) return "";
            Random ran = new Random();
            return Connectors[ran.Next(Connectors.Length)];
        }

        public enum SentenceType
        {
            Simple,
            Complex
        }
    }
}
