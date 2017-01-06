using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;

namespace DiscordBot
{
    public class SentenceHelpers
    {
        private static Sentence[] _loadedSentences;

        /// <summary>
        /// Updates the array of sentences with a new array.
        /// </summary>
        /// <param name="sentences">The sentences to assign.</param>
        public static void AssignSentences(Sentence[] sentences)
        {
            _loadedSentences = sentences;
        }

        /// <summary>
        /// Applies post processing to the message string for syntax based replacements.
        /// </summary>
        /// <param name="message">The message to post-process.</param>
        /// <param name="botUser">The user of the bot.</param>
        /// <param name="otherUser">The user who trigger the bot to speak.</param>
        /// <param name="server">The server that the bot is on.</param>
        /// <param name="channel">The channel that the server was called from.</param>
        /// <returns></returns>
        public static string PostProcessSyntax(string message, Profile botUser, User otherUser, Server server, Channel channel, string[] descriptors,
            string[] verbs, string[] nouns, string[] prefixes, string[] words, string[] pronouns)
        {

            // get users in the channel
            List<User> users = channel.Users.ToList();
            string[] mentions = new string[users.Count];
            for (int i = 0; i < mentions.Length; ++i) mentions[i] = users[i].Mention;

            // get random user
            User randomUser = users[new Random().Next(users.Count)];

            // replace indexed syntax
            message = ReplaceIndexSyntax(message, "verb", verbs);
            message = ReplaceIndexSyntax(message, "noun", nouns);
            message = ReplaceIndexSyntax(message, "prefix", prefixes);
            message = ReplaceIndexSyntax(message, "word", words);
            message = ReplaceIndexSyntax(message, "pronoun", pronouns);
            message = ReplaceIndexSyntax(message, "randomuser", mentions);
            message = ReplaceIndexSyntax(message, "descriptor", descriptors);

            // replace normal syntax
            message = ReplaceSyntax(message, "{verb}", verbs);
            message = ReplaceSyntax(message, "{noun}", nouns);
            message = ReplaceSyntax(message, "{prefix}", prefixes);
            message = ReplaceSyntax(message, "{word}", words);
            message = ReplaceSyntax(message, "{pronoun}", pronouns);
            message = ReplaceSyntax(message, "{randomuser}", mentions);
            message = ReplaceSyntax(message, "{descriptor}", descriptors);

            // inject random numbers
            string str = "{rnd}";
            int[] indicies = StringHelpers.AllIndexesOf(message, "{rnd}", true);
            for (int i = indicies.Length - 1; i >= 0; --i)
            {
                message = message.Remove(indicies[i], str.Length);
                message = message.Insert(indicies[i], $"{MathHelpers.GenerateRandomNumber(100)}");
            }

            // {name} syntax (bot name)
            message = message.Replace("{name}", botUser.Mention + " ");

            // {me} syntax (other user name)
            message = message.Replace("{me}", otherUser.Mention + " ");

            // {randomuser} syntax (random user name)
            //message = message.Replace("{randomuser}", randomUser.Mention);

            return message;
        }

        /// <summary>
        /// Loads a new sentence to append to the final message. This carries out all needed formatting.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public static string LoadSentence(int index)
        {
           StringBuilder sb = new StringBuilder();
            switch (_loadedSentences[index].Type)
            {
                case Sentence.SentenceType.Simple:
                    sb.Append(LoadSimpleSentence(_loadedSentences[index], false));
                    break;
                case Sentence.SentenceType.Complex:
                    sb.Append(LoadComplexSentence(_loadedSentences[index], 2, false));
                    break;
            }

            return sb.ToString();
        }

        /// <summary>
        /// Loads a simple sentence and returns the formatted result.
        /// </summary>
        /// <param name="sentence">The sentence to load.</param>
        /// <param name="connected">Whether this sentence is connected to the end of a complex sentence.</param>
        /// <returns></returns>
        private static string LoadSimpleSentence(Sentence sentence, bool connected)
        {
            string str = sentence.StrSentence.Trim(' ');
            str = StringHelpers.AutoCapitals(str, !connected);

            bool specialEnd = str.EndsWith("!") || str.EndsWith("?");
            return str + (!connected && !specialEnd ? ". " : specialEnd ? " " : "");
        }

        /// <summary>
        /// Loads a random simple sentence.
        /// </summary>
        /// <returns></returns>
        public static string LoadRandomSimpleSentence()
        {
            Sentence newSentence = _loadedSentences[MathHelpers.GenerateRandomNumber(_loadedSentences.Length)];
            while (newSentence.Type == Sentence.SentenceType.Complex) newSentence = _loadedSentences[MathHelpers.GenerateRandomNumber(_loadedSentences.Length)];

            return LoadSimpleSentence(newSentence, false);
        }

        /// <summary>
        /// Loads a complex sentence and returns the formatted result.
        /// </summary>
        /// <param name="sentence">The sentence to load.</param>
        /// <param name="maxChains">The number of times a complex sentence can be chained to the end of this one. 
        /// Note this value does not define how many times another complex sentence can be chained, just how many the system will allow before
        /// stopping anymore.</param>
        /// <param name="isChain">Whether this complex sentence is part of a chain. If true then this complex sentence cannot chain more
        /// complex sentences onto it.</param>
        /// <returns></returns>
        private static string LoadComplexSentence(Sentence sentence, int maxChains, bool isChain)
        {
            StringBuilder sb = new StringBuilder();

            // get sentence and connector
            string str = sentence.StrSentence;
            string connector = sentence.GetRandomConnector();

            // append sentence
            sb.Append(str.Trim(' '));
            sb.Append(" ");
            sb.Append(connector.Trim(' '));
            sb.Append(" ");

            // if not the child of a chain then start chaining more sentences onto this one
            if (isChain) return sb.ToString();

            // load chain of complex sentences
            int chainCount = MathHelpers.GenerateRandomNumber(maxChains);

            Sentence newSentence = null;
            Sentence prevSentence = sentence;
            for (int i = 0; i < chainCount; ++i)
            {
                // load new sentence
                newSentence = _loadedSentences[MathHelpers.GenerateRandomNumber(_loadedSentences.Length)];
                while (newSentence.Type == Sentence.SentenceType.Simple 
                       /*|| newSentence == prevSentence */
                       || newSentence == sentence) newSentence = _loadedSentences[MathHelpers.GenerateRandomNumber( _loadedSentences.Length)];
                prevSentence = newSentence;

                // append it 
                sb.Append(LoadComplexSentence(newSentence, 0, true));
            }

            // finally load simple sentence to finish off chain
            newSentence = _loadedSentences[MathHelpers.GenerateRandomNumber(_loadedSentences.Length)];

            while (newSentence.Type == Sentence.SentenceType.Complex) newSentence = _loadedSentences[MathHelpers.GenerateRandomNumber(_loadedSentences.Length)];

            // append sentence
            sb.Append(LoadSimpleSentence(newSentence, true));
            sb.Append(". ");
            return sb.ToString();
        }

        /// <summary>
        /// Replaces any instances of syntax with a random string from the replacements array.
        /// </summary>
        /// <param name="message">The message to manipulate.</param>
        /// <param name="syntax">The syntax to replace.</param>
        /// <param name="replacements">The array of replacements.</param>
        /// <returns></returns>
        public static string ReplaceSyntax(string message, string syntax, string[] replacements)
        {
            int[] indicies = StringHelpers.AllIndexesOf(message, syntax, true);
            if (indicies.Length <= 0) return message;

            for (int i = indicies.Length - 1; i >= 0; --i)
            {
                message = message.Remove(indicies[i], syntax.Length);
                message = message.Insert(indicies[i], replacements[MathHelpers.GenerateRandomNumber(replacements.Length)].Trim(' '));
            }
            return message;
        }

        /// <summary>
        /// Replaces any instances of indexed syntaxes with a random string from the replacements array.
        /// </summary>
        /// <param name="message">The message to manipulate.</param>
        /// <param name="syntax">The syntax to replace.</param>
        /// <param name="replacements">The array of replacements.</param>
        /// <returns></returns>
        public static string ReplaceIndexSyntax(string message, string syntax, string[] replacements)
        {
            // find where all instances of the syntax start
            int[] indiciesOpen = StringHelpers.AllIndexesOf(message, "{" + syntax, true);

            // copy start locations into new syntax
            List<IndexedSyntax> iSyntax = indiciesOpen.Select(t => new IndexedSyntax {StartLocation = t}).ToList();

            // copy string into char array
            char[] charArray = message.ToCharArray();

            // find where all syntax locations end
            for (int i = 0; i < iSyntax.Count; ++i)
            {
                // find final index
                int finalIndex = iSyntax[i].StartLocation;
                for (int j = iSyntax[i].StartLocation; j < charArray.Length; ++j)
                {
                    if (charArray[j] != '}') continue;

                    finalIndex = j;
                    break;
                }
                iSyntax[i].Length = finalIndex - iSyntax[i].StartLocation;

                // figure out if this syntax is a normal syntax
                if (charArray[iSyntax[i].StartLocation + syntax.Length + 1] == '}') iSyntax[i].IsNormalSyntax = true;
            }

            // figure out what each syntax index is
            List<int> indicies = new List<int>();
            for (int i = 0; i < iSyntax.Count; ++i)
            {
                if (iSyntax[i].IsNormalSyntax) continue;

                List<char> readChars = new List<char>();

                int len = syntax.Length + 1;
                for (int j = iSyntax[i].StartLocation + len; j < iSyntax[i].StartLocation + len + (iSyntax[i].Length - len); ++j)
                    readChars.Add(charArray[j]);

                StringBuilder sb = new StringBuilder("");
                for (int j = 0; j < readChars.Count; ++j) sb.Append(readChars[j]);

                int index = -1;
                bool parsed = int.TryParse(sb.ToString(), out index);
                if (parsed) iSyntax[i].Index = index;
            }

            // figure out how many indicies there are
            int indexCount = -1;
            indicies = new List<int>();
            for (int i = 0; i < iSyntax.Count; ++i)
            {
                if (iSyntax[i].Index <= indexCount) continue;

                indexCount = iSyntax[i].Index;
                indicies.Add(iSyntax[i].Index);
            }

            // generate new replacements for each index
            IndexSyntaxReplacement[] rSyntax = new IndexSyntaxReplacement[indicies.Count];
            for (int i = 0; i < rSyntax.Length; ++i)
            {
                rSyntax[i] = new IndexSyntaxReplacement
                {
                    Index = indicies[i],
                    Replacement = replacements[MathHelpers.GenerateRandomNumber(replacements.Length)].Trim(' ')
                };
            }

            // swap out syntax
            for (int i = iSyntax.Count - 1; i >= 0; --i)
            {
                if (iSyntax[i].IsNormalSyntax || iSyntax[i].Index == -1) continue;

                for (int j = 0; j < rSyntax.Length; ++j)
                {
                    if (iSyntax[i].Index != rSyntax[j].Index) continue;

                    message = message.Remove(iSyntax[i].StartLocation, iSyntax[i].Length + 1);
                    message = message.Insert(iSyntax[i].StartLocation, rSyntax[j].Replacement);
                }
            }

            return message;
        }

        /// <summary>
        /// Clears the array of sentences.
        /// </summary>
        public static void ClearSentences()
        {
            _loadedSentences = null;
        }

    }

    public class IndexedSyntax
    {
        public bool IsNormalSyntax;
        public int StartLocation;
        public int Length;
        public int Index = -1;
    }

    public class IndexSyntaxReplacement
    {
        public int Index = -1;
        public string Replacement;
    }
}
