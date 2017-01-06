using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Audio;
using Discord.Commands;
using System.IO;
using System.Threading;
using Discord.Modules;
using NAudio.Wave;

namespace DiscordBot
{
    class Program
    {
        // loaded settings
        public string BotToken;

        public static DiscordClient Client;
        private static IAudioClient _audio;

        private static CommandService _commands;

        // files
        private static readonly string SentenceFile = Environment.CurrentDirectory + "/speak/sentences.txt";
        private static readonly string DescriptorFile = Environment.CurrentDirectory + "/speak/descriptors.txt";
        private static readonly string VerbsFile = Environment.CurrentDirectory + "/speak/verbs.txt";
        private static readonly string NounFile = Environment.CurrentDirectory + "/speak/nouns.txt";
        private static readonly string PrefixFile = Environment.CurrentDirectory + "/speak/prefixes.txt";
        private static readonly string WordsFile = Environment.CurrentDirectory + "/speak/words.txt";
        private static readonly string PronounsFile = Environment.CurrentDirectory + "/speak/pronouns.txt";

        private static CancellationTokenSource ts;
        private static CancellationToken ct;

        static void Main(string[] args)
        {
            Settings.LoadSettings();

            Client = new DiscordClient();
            SetupCommands();

            Client.MessageReceived += MessageRecieved;
            Client.ExecuteAndWait(async () =>
            {
                await Client.Connect(Settings.BotToken, TokenType.Bot);
                Client.SetGame(Settings.BotGame);
                Client.UsingModules();
                Client.UsingAudio(x =>
                {
                    x.Mode = AudioMode.Outgoing;
                    x.EnableEncryption = false;
                    x.Bitrate = 128;
                    x.BufferLength = 10000;
                });
            });
        }

        private static void SetupCommands()
        {
            CommandServiceConfigBuilder csBuilder = new CommandServiceConfigBuilder
            {
                AllowMentionPrefix = true,
                HelpMode = HelpMode.Public,
                PrefixChar = '-'
            };
            CommandService cs = new CommandService(csBuilder.Build());
            _commands = Client.AddService(cs);

            _commands.CreateCommand("play")
                .Description("Play a sound.")
                .Parameter("Sound Name", ParameterType.Unparsed)
                .Do(async e =>
                {
                    if (!e.Channel.Name.Contains("bot")) return;

                    string[] sounds = e.GetArg("Sound Name").Split(' ');
                    if (sounds.Length > 0)
                    {
                        Func<Task> playSounds = async () =>
                        {
                            await ConnectToVoice();
                            if (_audio == null) return;

                            for (int i = 0; i < sounds.Length; ++i)
                            {
                                if (File.Exists(StringHelpers.GetSoundLocation(sounds[i])))
                                {
                                    //await PlaySound(StringHelpers.GetSoundLocation(sounds[i]));
                                    ts = new CancellationTokenSource();
                                    ct = ts.Token;
                                    await Task.Run(() =>
                                    {
                                        try
                                        {
                                            using (WaveFileReader reader = new WaveFileReader(StringHelpers.GetSoundLocation(sounds[i])))
                                            {
                                                WaveFormat format = new WaveFormat(48000, 16, 2);
                                                int length = Convert.ToInt32(format.AverageBytesPerSecond / 60.0 * 1000.0);
                                                byte[] buffer = new byte[length];

                                                using (WaveFormatConversionStream resampler = new WaveFormatConversionStream(format, reader))
                                                {
                                                    int count = 0;
                                                    while ((count = resampler.Read(buffer, 0, length)) > 0)
                                                    {
                                                        _audio.Send(buffer, 0, count);
                                                    }
                                                }

                                                _audio.Wait();
                                            }
                                        }
                                        catch
                                        {
                                            Console.WriteLine("Playing Failed");
                                        }
                                    }, ct);
                                }
                                else
                                {
                                    await e.Channel.SendMessage($"Sound {sounds[i]} not exist. Use listsounds command to get list of available sounds.");
                                }
                            }
                        };

                        playSounds().Start();
                    }
                    else
                    {
                        await e.Channel.SendMessage("You didn't specify any sounds.");
                    }
                });

            _commands.CreateCommand("sounds")
                .Description("Lists all of the available sounds.")
                .Parameter("command", ParameterType.Required)
                .Parameter("start", ParameterType.Required)
                .Parameter("count", ParameterType.Required)
                .Do(e =>
                {
                    if (!e.Channel.Name.Contains("bot")) return;

                    // get sound folder
                    string dir = Environment.CurrentDirectory + "/sounds/";
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                    // find files
                    DirectoryInfo di = new DirectoryInfo(Environment.CurrentDirectory + "/sounds/");
                    FileSystemInfo[] fsi = di.GetFileSystemInfos();

                    if (fsi == null)
                    {
                        e.Channel.SendMessage("No sound files found.");
                    }
                    else
                    {
                        FileSystemInfo[] orderedFiles = new FileSystemInfo[0];
                        switch (e.GetArg("command"))
                        {
                            case "la":
                                orderedFiles = fsi.OrderBy(f => f.CreationTime).ToArray();
                                break;

                            case "ld":
                                orderedFiles = fsi.OrderByDescending(f => f.CreationTime).ToArray();
                                break;

                            case "ln":
                                orderedFiles = fsi.OrderBy(f => f.Name).ToArray();
                                break;
                        }

                        int start = 0;
                        int count = 0;
                        bool parsedStart = int.TryParse(e.GetArg("start"), out start);
                        bool parsedCount = int.TryParse(e.GetArg("count"), out count);

                        if (!parsedStart)
                        {
                            e.Channel.SendMessage("Could not parse start param, provide a number.");
                            return;
                        }

                        if (!parsedCount)
                        {
                            e.Channel.SendMessage("Could not parse count param, provide a number.");
                            return;
                        }

                        if (start > orderedFiles.Length - 1)
                        {
                            e.Channel.SendMessage($"The provided start number is higher then the number of files ({orderedFiles.Length} zero based)");
                        }

                        if (count > orderedFiles.Length - 1)
                        {
                            e.Channel.SendMessage($"The provided count number is higher then the number of files ({orderedFiles.Length} zero based)");
                        }

                        if (orderedFiles.Length == 0) e.Channel.SendMessage("No sound files found.");
                        else
                        {
                            StringBuilder sb = new StringBuilder();
                            sb.Append("Available Sounds: \n");
                            for (int i = start; i < start + count && i < orderedFiles.Length; ++i)
                            {
                                FileInfo newFI = new FileInfo(orderedFiles[i].Name);
                                sb.Append(newFI.Name.Replace(".wav", ""));
                                sb.Append("\n");
                            }
                            e.Channel.SendMessage(sb.ToString());
                        }
                    }
                });

            _commands.CreateCommand("listsentences")
                .Alias("sd")
                .Description("Lists of all the sentences learned within the set range.")
                .Parameter("start", ParameterType.Required)
                .Parameter("count", ParameterType.Required)
                .Do(e =>
                {
                    if (!e.Channel.Name.Contains("bot")) return;

                    // try to parse the start and count params
                    int start = 0;
                    int count = 0;
                    bool parsed = int.TryParse(e.GetArg("start"), out start);

                    if (!parsed)
                    {
                        e.Channel.SendTTSMessage("Could not parse start param");
                        return;
                    }

                    parsed = int.TryParse(e.GetArg("count"), out count);
                    if (!parsed)
                    {
                        e.Channel.SendTTSMessage("Could not parse count param");
                        return;
                    }

                    // load sentences from disk
                    Sentence[] sentences = ReadSentences();

                    if (start > sentences.Length)
                    {
                        e.Channel.SendTTSMessage($"Start index is higher then sentences length of {sentences.Length}");
                        return;
                    }

                    if (start + count > sentences.Length)
                    {
                        e.Channel.SendTTSMessage($"Search covers indexs that are higher then sentences length of {sentences.Length}");
                        return;
                    }

                    StringBuilder sb = new StringBuilder("Stored Sentences: \n");
                    for (int i = start; i < start + count; ++i)
                    {
                        sb.Append($"***Index {i}*** \n");
                        sb.Append($"    Sentence Type: {sentences[i].Type}\n");
                        sb.Append($"    Sentence Text: {sentences[i].StrSentence}\n");
                        if (sentences[i].Connectors.Length > 0) sb.Append($"    Sentence Connectors: \n");

                        for (int j = 0; j < sentences[i].Connectors.Length; ++j)
                        {
                            sb.Append($"         * {sentences[i].Connectors[j]}\n");
                        }
                    }

                    e.Channel.SendMessage(sb.ToString().Length > 2000 ? "Output message too long" : sb.ToString());
                });

            _commands.CreateCommand("speaktest")
                .Alias("st")
                .Description("Sends a single sentence over.")
                .Parameter("Index", ParameterType.Required)
                .Do(e =>
                {
                    int index = 0;
                    bool parsed = int.TryParse(e.GetArg("Index"), out index);
                    if (!parsed)
                    {
                        e.Channel.SendMessage("Could not parse integer from param.");
                        return;
                    }

                    // load sentences from disk
                    Sentence[] sentences = ReadSentences();
                    if (index >= sentences.Length)
                    {
                        e.Channel.SendMessage($"Provided index is higher then number of sentences ({sentences.Length})");
                        return;
                    }

                    SentenceHelpers.AssignSentences(sentences);
                    string message = SentenceHelpers.PostProcessSyntax(SentenceHelpers.LoadSentence(index), Client.CurrentUser, e.User, e.Server, e.Channel,
                        ReadLinesFromFile(DescriptorFile), ReadLinesFromFile(VerbsFile), ReadLinesFromFile(NounFile), ReadLinesFromFile(PrefixFile), ReadLinesFromFile(WordsFile),
                        ReadLinesFromFile(PronounsFile));

                    bool useTts = new Random().Next(100) > 75;
                    if (useTts) e.Channel.SendTTSMessage(message.Length > 2000 ? "Message exceeds 2000 character limit." : message);
                    else e.Channel.SendMessage(message.Length > 2000 ? "Message exceeds 2000 character limit." : message);
                    SentenceHelpers.ClearSentences();
                });

            _commands.CreateCommand("learn")
                .Alias("l")
                .Description("Makes bot learn a new sentence.")
                .Parameter("Sentence", ParameterType.Unparsed)
                .Do(e =>
                {
                    string learnFrom = e.GetArg("Sentence");
                    string[] lines = learnFrom.Split('|');

                    if (lines[0] == "desc ")
                    {
                        WriteLineToFile(lines[1], DescriptorFile);

                        e.Channel.SendTTSMessage($"Learned new descriptor: {lines[1]}");
                    } else if (lines[0] == "verb ")
                    {
                        WriteLineToFile(lines[1], VerbsFile);

                        e.Channel.SendTTSMessage($"Learned new verb: {lines[1]}");
                    } else if (lines[0] == "noun ")
                    {
                        WriteLineToFile(lines[1], NounFile);

                        e.Channel.SendTTSMessage($"Learned new noun: {lines[1]}");
                    } else if (lines[0] == "prefix ")
                    {
                        WriteLineToFile(lines[1], PrefixFile);

                        e.Channel.SendTTSMessage($"Learned new prefix: {lines[1]}");
                    } else if (lines[0] == "word ")
                    {
                        WriteLineToFile(lines[1], WordsFile);

                        e.Channel.SendTTSMessage($"Learned new word: {lines[1]}");
                    } else if (lines[0] == "pronoun ")
                    {
                        WriteLineToFile(lines[1], PronounsFile);

                        e.Channel.SendTTSMessage($"Learned new pronoun: {lines[1]}");
                    }
                    else
                    {
                        Sentence newSentence = new Sentence();
                        if (lines[0] == "simple ") newSentence.Type = Sentence.SentenceType.Simple;
                        if (lines[0] == "complex ") newSentence.Type = Sentence.SentenceType.Complex;

                        newSentence.StrSentence = lines[1];

                        if (newSentence.Type == Sentence.SentenceType.Simple)
                        {
                            newSentence.Connectors = new string[0];
                        }
                        else
                        {
                            string[] connectors = lines[2].Split(',');
                            newSentence.Connectors = new string[connectors.Length];
                            for (int i = 0; i < newSentence.Connectors.Length; ++i) newSentence.Connectors[i] = connectors[i].Remove(0, 1);
                        }

                        // send message about what has just been learned
                        StringBuilder sb = new StringBuilder("");
                        sb.Append("Learned: \n");
                        sb.Append("Sentence Type: ");
                        sb.Append(newSentence.Type == Sentence.SentenceType.Simple ? "Simple \n" : "Complex \n");
                        sb.Append(newSentence.StrSentence + "\n");
                        sb.Append(newSentence.Connectors.Length + " connectors. \n");
                        for (int i = 0; i < newSentence.Connectors.Length; ++i)
                        {
                            sb.Append(newSentence.Connectors[i] + "\n");
                        }
                        e.Channel.SendTTSMessage(sb.ToString());

                        SaveNewSentence(newSentence);
                    }
                });

            _commands.CreateCommand("speak")
                .Alias("s")
                .Description("Makes bot chain together a series of sentences.")
                .Parameter("Count", ParameterType.Required)
                .Do(async e =>
                {
                    // load sentences from disk
                    Sentence[] sentences = ReadSentences();

                    // try to parse the count argument
                    // if it can't be parsed to an integer or the parsed value is lower then the desired count then let people know
                    int count;
                    bool parsed = int.TryParse(e.GetArg("Count"), out count);

                    if (!parsed)
                    {
                        await e.Channel.SendMessage("Could not parse count argument. Provide a number.");
                        return;
                    }
                    if (sentences.Length < count)
                    {
                        await e.Channel.SendMessage("You asked for more sentences then are available, provide a lower number.");
                        return;
                    }

                    // get the indicies for the random sentences to append
                    Random rand = new Random();
                    int[] indicies = MathHelpers.GetRandomIndicies(count, sentences.Length);

                    // load sentences into the helper class
                    SentenceHelpers.AssignSentences(sentences);

                    // string together sentences
                    StringBuilder sb = new StringBuilder(" ");
                    for (int i = 0; i < count; ++i) sb.Append(SentenceHelpers.LoadSentence(indicies[i]));

                    // post process the final message to send
                    string processedMessage = SentenceHelpers.PostProcessSyntax(sb.ToString(), Client.CurrentUser, e.User, e.Server, e.Channel,
                        ReadLinesFromFile(DescriptorFile), ReadLinesFromFile(VerbsFile), ReadLinesFromFile(NounFile), ReadLinesFromFile(PrefixFile), ReadLinesFromFile(WordsFile),
                        ReadLinesFromFile(PronounsFile));

                    if (processedMessage.Length > 2000)
                    {
                        await e.Channel.SendMessage("Message exceeds 2000 character limit.");
                    }
                    else
                    {
                        // send message with random tts chance
                        bool useTts = rand.Next(100) > 80;
                        if (useTts) await e.Channel.SendTTSMessage(processedMessage);
                        else await e.Channel.SendMessage(processedMessage);
                    }

                    bool sendPm = rand.Next(100) > 80;
                    if (sendPm)
                    {
                        Channel pChannel = await e.User.CreatePMChannel().ConfigureAwait(false);
                        await pChannel.SendMessage(SentenceHelpers.PostProcessSyntax(SentenceHelpers.LoadRandomSimpleSentence(), Client.CurrentUser, e.User, e.Server, e.Channel,
                        ReadLinesFromFile(DescriptorFile), ReadLinesFromFile(VerbsFile), ReadLinesFromFile(NounFile), ReadLinesFromFile(PrefixFile), ReadLinesFromFile(WordsFile),
                        ReadLinesFromFile(PronounsFile)));
                    }

                    // clear sentences from helper class
                    SentenceHelpers.ClearSentences();
                });

            _commands.CreateCommand("speakcount")
                .Alias("sc")
                .Description("Returns the number of sentences that bot has learned.")
                .Parameter("Type", ParameterType.Optional)
                .Do(e =>
                {
                    Sentence[] sentences = ReadSentences();

                    if (sentences == null) e.Channel.SendMessage("No sentences could be loaded.");

                    string arg = e.GetArg("Type").ToLower();
                    if (arg == "simple")
                    {
                        int count = sentences.Count(t => t.Type == Sentence.SentenceType.Simple);
                        e.Channel.SendMessage($"Found {count} simple sentences");

                    } else if (arg == "complex")
                    {
                        int count = sentences.Count(t => t.Type == Sentence.SentenceType.Complex);
                        e.Channel.SendMessage($"Found {count} complex sentences");

                    } else e.Channel.SendMessage($"Found {sentences.Length} sentences");
                });

            _commands.CreateCommand("stop")
                .Description("Stops any current audio playback.")
                .Do(e =>
                {
                    ts?.Cancel();
                });

            _commands.CreateCommand("spamroulette")
                .Description("Sends a standard bot speak to a random person in a random persons friendslist.")
                .Do(async e =>
                {
                    // load sentences from disk
                    Sentence[] sentences = ReadSentences();

                    SentenceHelpers.AssignSentences(sentences);
                    string message = SentenceHelpers.PostProcessSyntax(SentenceHelpers.LoadSentence(MathHelpers.GenerateRandomNumber(sentences.Length)), Client.CurrentUser, e.User, e.Server, e.Channel,
                        ReadLinesFromFile(DescriptorFile), ReadLinesFromFile(VerbsFile), ReadLinesFromFile(NounFile), ReadLinesFromFile(PrefixFile), ReadLinesFromFile(WordsFile),
                        ReadLinesFromFile(PronounsFile));

                    User[] currentUsers = e.Channel.Users.ToArray();
                    User targetUser = currentUsers[MathHelpers.GenerateRandomNumber(currentUsers.Length)];
                    Channel pChannel = await targetUser.CreatePMChannel().ConfigureAwait(false);
                    await pChannel.SendMessage($"Congratulations on being chosen in spam roulette on {e.Channel.Server.Name}! \n {message}");

                    bool warning = new Random().Next(100) > 50;
                    await e.Channel.SendMessage($"{targetUser.Mention} was chosen, nice!");
                    if (warning) await pChannel.SendMessage($"Send that message to {MathHelpers.GenerateRandomNumber(10)} people in your friends list or your mother will die in her sleep.");
                    SentenceHelpers.ClearSentences();

                });

            QuestionTime.RegisterQuestiontimeCommands(_commands);
        }

        private static void OnCommandExecuted(object sender, CommandEventArgs e)
        {
            Console.WriteLine("Meme");
        }

        private static void OnCommandError(object sender, CommandErrorEventArgs e)
        {

        }

        static void MessageRecieved(object sender, MessageEventArgs e)
        {
            if (!e.Channel.Name.Contains("bot")) return;

        }

        static async Task ConnectToVoice()
        {
            if (_audio != null)
            {
                if (_audio.State != ConnectionState.Disconnected) return;
            }

            Server svr = Client.Servers.First();
            Channel channel = svr.FindChannels("General", ChannelType.Voice, false).FirstOrDefault();

            _audio = await channel.JoinAudio();
        }

        static Sentence[] ReadSentences()
        {
            if (!File.Exists(SentenceFile)) File.Create(SentenceFile);

            using (Stream stream = File.Open(SentenceFile, FileMode.Open))
            {
                using (StreamReader reader = new StreamReader(stream))
                {
                    if (stream.Length == 0) return null;

                    // read sentences
                    List<Sentence> sentences = new List<Sentence>();
                    while (true)
                    {
                        // read sentence header
                        Sentence newSentence = new Sentence();

                        string str = reader.ReadLine();
                        if (str != "Sentence") break;

                        int type;
                        bool parsed = int.TryParse(reader.ReadLine(), out type);
                        if (!parsed) return null;

                        newSentence.Type = (Sentence.SentenceType) type;
                        newSentence.StrSentence = reader.ReadLine();

                        int connectorCount = 0;
                        parsed = int.TryParse(reader.ReadLine(), out connectorCount);
                        newSentence.Connectors = new string[connectorCount];
                        if (parsed)
                        {
                            for (int i = 0; i < connectorCount; ++i) newSentence.Connectors[i] = reader.ReadLine();
                        }

                        sentences.Add(newSentence);
                    }
                    return sentences.ToArray();
                }
            }
        }

        static void SaveNewSentence(Sentence sentence)
        {
            if (!File.Exists(SentenceFile)) File.Create(SentenceFile);
            using (Stream stream = File.Open(SentenceFile, FileMode.Open))
            {
                using (StreamWriter writer = new StreamWriter(stream))
                {
                    stream.Position = stream.Length;

                    // write sentence
                    writer.WriteLine("Sentence");
                    writer.WriteLine((int)sentence.Type);
                    writer.WriteLine(sentence.StrSentence);
                    writer.WriteLine(sentence.Connectors.Length);
                    for (int i = 0; i < sentence.Connectors.Length; ++i) writer.WriteLine(sentence.Connectors[i]);
                }
            }
        }

        public static string[] ReadLinesFromFile(string file)
        {
            return !File.Exists(file) ? new string[1] { "" } : File.ReadAllLines(file);
        }

        public static void WriteLineToFile(string line, string file)
        {
            FileInfo fi = new FileInfo(file);
            if (!Directory.Exists(fi.DirectoryName)) if (fi.DirectoryName != null) Directory.CreateDirectory(fi.DirectoryName);
            using (Stream stream = File.Open(file, FileMode.OpenOrCreate))
            {
                using (StreamWriter writer = new StreamWriter(stream))
                {
                    stream.Position = stream.Length;

                    writer.WriteLine(line);
                }
            }
        }
    }
}
