using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using Discord;
using Discord.Commands;
using RestSharp.Extensions;

namespace DiscordBot
{
    public class QuestionTime
    {
        // settings
        private static string _questionsFile = Environment.CurrentDirectory + "/questiontime/questions.txt";
        private static int _waitingPlayerTime = 30;
        private static int _waitingAnswerTime = 60;
        private static int _waitingVoteTime = 45;

        // game variables
        private static bool _usingBot;
        private static int _aiCount;
        private static string _question;
        public static QuestionTimeState CurrentState = QuestionTimeState.Inactive;
        public static List<User> CurrentUsers = new List<User>();
        public static List<QtVote> CurrentVotes = new List<QtVote>();

        // timers
        private static Timer _waitingPlayerTimer;
        private static int _waitingPlayerTicks;

        private static Timer _waitingReplyTimer;
        private static int _waitingReplyTicks;

        private static Timer _waitingVoteTimer;
        private static int _waitingVoteTicks;

        // messages
        private static Channel _gameChannel;
        private static Channel _answerOneChannel;
        private static bool _answerOneReplied;
        private static string _answerOne;
        private static Channel _answerTwoChannel;
        private static bool _answerTwoReplied;
        private static string _answerTwo;
        private static Message _messageWaiting;

        public static void RegisterQuestiontimeCommands(CommandService commands)
        {
            commands.CreateCommand("qlearn")
                .Description("Makes bot learn a new question for question time")
                .Parameter("question", ParameterType.Unparsed)
                .Do(e =>
                {
                    string question = e.GetArg("question");
                    if (question == "" || question == " ")
                    {
                        e.Channel.SendMessage("Please provide a question.");
                        return;
                    }
                    else
                    {
                        if (!question.EndsWith("?")) question += "?";

                        Program.WriteLineToFile(question, _questionsFile);
                        e.Channel.SendTTSMessage($"Learned new QT question: {question}");
                    }
                });

            commands.CreateCommand("qstart")
                .Description("Starts question time.")
                .Parameter("usingBot", ParameterType.Required)
                .Parameter("aiCount", ParameterType.Required)
                .Do(async e =>
                {
                    if (CurrentState == QuestionTimeState.Inactive)
                    {
                        // store reference to channel
                        _gameChannel = e.Channel;

                        // send mesages
                        _usingBot = e.GetArg("usingBot") == "true";

                        _aiCount = 0;
                        bool parsed = int.TryParse(e.GetArg("aiCount"), out _aiCount);
                        if (!parsed)
                        {
                            await e.Channel.SendMessage("Could not parse AI count, provide a number.");
                            return;
                        }

                        await e.Channel.SendTTSMessage($"Question Time has started. Type @{Program.Client.CurrentUser.Name} qtime join to participate. Using Bot: {_usingBot}, using AI: {_aiCount}");
                        _messageWaiting = await e.Channel.SendMessage($"Game starts in {_waitingPlayerTime} seconds.");

                        // register delegate
                        Program.Client.MessageReceived += MessageRecieved;

                        // set state and start timer
                        CurrentState = QuestionTimeState.AwaitingPeople;
                        _waitingPlayerTimer = new Timer(1000);
                        _waitingPlayerTimer.Elapsed += AwaitingPeopleTick;
                        _waitingPlayerTimer.Start();

                    }
                    else await e.Channel.SendMessage("Question Time is already started, wait until the current run is over then run again.");
                });

            commands.CreateCommand("qtime")
                .Description("Question time commands.")
                .Parameter("action", ParameterType.Required)
                .Do(e =>
                {
                    if (CurrentState == QuestionTimeState.Inactive)
                    {
                        e.Channel.SendMessage($"{e.User.Mention} question time is currently not running. Run qstart to run QT commands.");
                        return;
                    }

                    string action = e.GetArg("action");
                    switch (action)
                    {
                        case "":
                            e.Channel.SendMessage($"{e.User.Mention} you did not provide a command.");
                            break;
                        case "join":
                            if (CurrentState != QuestionTimeState.AwaitingPeople) return;

                            bool alreadyAdded = AddUser(e.User);
                            e.Channel.SendTTSMessage(alreadyAdded ? $"{e.User.Mention} you have already been added for this round." : $"{e.User.Mention} you have been added for this round.");
                            break;
                    }
                });

            commands.CreateCommand("qvote")
                .Description("Votes for an answer in question time.")
                .Parameter("vote", ParameterType.Required)
                .Do(e =>
                {
                    if (CurrentState != QuestionTimeState.AwaitingVote)
                    {
                        e.Channel.SendMessage("Question Time is rather not running or the game has not reached the voting stage yet.");
                        return;
                    }

                    bool inList = UserInList(e.User);
                    if (!inList)
                    {
                        e.Channel.SendMessage($"{e.User.Mention} you are not playing in this round and cannot vote.");
                        return;

                    }

                    bool alreadyVoted = UserAlreadyVoted(e.User);
                    if (alreadyVoted)
                    {
                        e.Channel.SendMessage($"{e.User.Mention} you have already voted for this round");
                        return;
                    }

                    string vote = e.GetArg("vote");
                    if (vote == "a" || vote == "A")
                    {
                        UserVote(QuestionTimeVoteType.A, e.User);
                        e.Channel.SendMessage($"{e.User.Mention} thank you for voting.");
                        e.Message.Delete();
                    } else if (vote == "b" || vote == "B")
                    {
                        UserVote(QuestionTimeVoteType.B, e.User);
                        e.Channel.SendMessage($"{e.User.Mention} thank you for voting.");
                        e.Message.Delete();
                    }
                    else
                    {
                        e.Channel.SendMessage($"{e.User.Mention} that is not a valid vote. Vote with rather a or b");
                    }
                });
        }

        /// <summary>
        /// Adds a new user to the current user list if they havn't already been.
        /// </summary>
        /// <param name="user">The user to add.</param>
        /// <returns></returns>
        public static bool AddUser(User user)
        {
            bool userAlreadyAdded = UserInList(user);
            if (!userAlreadyAdded) CurrentUsers.Add(user);
            return userAlreadyAdded;
        }

        /// <summary>
        /// Checks to see if a user is already in the user list.
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public static bool UserInList(User user)
        {
            bool inList = false;
            for (int i = 0; i < CurrentUsers.Count; ++i)
            {
                if (CurrentUsers[i] == user) inList = true;
            }

            return inList;
        }

        /// <summary>
        /// Adds a users vote.
        /// </summary>
        /// <param name="vote"></param>
        /// <param name="user"></param>
        public static void UserVote(QuestionTimeVoteType vote, User user)
        {
            QtVote newVote = new QtVote();
            newVote.VoteUser = user;
            newVote.VoteCast = vote;

            CurrentVotes.Add(newVote);
        }

        /// <summary>
        /// Checks if a user has already voted.
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public static bool UserAlreadyVoted(User user)
        {
            bool voted = false;
            for (int i = 0; i < CurrentVotes.Count; ++i)
            {
                if (CurrentVotes[i].VoteUser == user) voted = true;
            }
            return voted;
        }

        /// <summary>
        /// Called everytime the waiting player timer ticks.
        /// </summary>
        public static async void AwaitingPeopleTick(object obj, ElapsedEventArgs args)
        {
            if (_messageWaiting == null) return;
            if (_waitingPlayerTicks == _waitingPlayerTime)
            {
                // delete countdown message and choose random people to play.
                await _messageWaiting.Delete();

                int userCount = CurrentUsers.Count;
                int requiredPlayers = 2;

                // if there are not enough players then stop the game.
                if (userCount == 0)
                {
                    await _gameChannel.SendTTSMessage($"Nobody joined Question Time, resetting.");
                    ResetQuestionTime();
                } else if (userCount < requiredPlayers)
                {
                    await _gameChannel.SendTTSMessage($"Not enough people joined (3 min required). Resetting Question Time. Use @{Program.Client.CurrentUser.Name} qstart botenabled aiCount for automated gameplay.");
                    ResetQuestionTime();
                } else if (userCount >= requiredPlayers)
                {
                    // choose two random players to participate
                    User userOne = CurrentUsers[MathHelpers.GenerateRandomNumber(userCount)];

                    User userTwo = userOne;
                    while (userTwo == userOne) userTwo = CurrentUsers[MathHelpers.GenerateRandomNumber(userCount)];

                    // create new channels
                    _answerOneChannel = await userOne.CreatePMChannel().ConfigureAwait(false);
                    _answerTwoChannel = await userTwo.CreatePMChannel().ConfigureAwait(false);

                    await _gameChannel.SendTTSMessage($"{userOne.Mention} and {userTwo.Mention} you have been chosen to provide the question answers. Reply to {Program.Client.CurrentUser.Name}'s PM with @{Program.Client.CurrentUser.Name} qanswer in the next {_waitingAnswerTime} seconds.");

                    string[] questions = Program.ReadLinesFromFile(_questionsFile);
                    _question = questions[MathHelpers.GenerateRandomNumber(questions.Length)];

                    // send PMs to users
                    await _answerOneChannel.SendMessage($"The question is: {_question}. \n \n Enter your answer with @{Program.Client.CurrentUser.Name} qanswer.");
                    await _answerTwoChannel.SendMessage($"The question is: {_question}. \n \n Enter your answer with @{Program.Client.CurrentUser.Name} qanswer.");
                    _messageWaiting = await _gameChannel.SendMessage($"Round will start in {_waitingAnswerTime} seconds.");

                    // set state and start timer
                    CurrentState = QuestionTimeState.AwaitingQuestionAnswers;
                    _waitingReplyTimer = new Timer(1000);
                    _waitingReplyTimer.Elapsed += AwaitingReplyTick;
                    _waitingReplyTimer.Start();
                }

                _waitingPlayerTimer.Stop();
                _waitingPlayerTimer = null;
                return;
            }

            ++_waitingPlayerTicks;
            await _messageWaiting.Edit($"Game starts in {_waitingPlayerTime - _waitingPlayerTicks} seconds.");
        }

        /// <summary>
        /// Called everytime the waiting answer timer ticks.
        /// </summary>
        public static async void AwaitingReplyTick(object obj, ElapsedEventArgs args)
        {
            if (_answerOneReplied && _answerTwoReplied)
            {
                // send 
                await _messageWaiting.Delete();
                await _gameChannel.SendMessage($"The question is: {_question}");
                await _gameChannel.SendMessage($"The possible answers are: a) {_answerOne} or b) {_answerTwo}");
                _messageWaiting = await _gameChannel.SendMessage($"Round will end in {_waitingAnswerTime} seconds.");

                await _gameChannel.SendTTSMessage($"Voting has began. Enter your vote now with @{Program.Client.CurrentUser.Name} qvote A or @{Program.Client.CurrentUser.Name} qvote B");

                // set state and start timer
                CurrentState = QuestionTimeState.AwaitingVote;
                _waitingVoteTimer = new Timer(1000);
                _waitingVoteTimer.Elapsed += AwaitingVoteTick;
                _waitingVoteTimer.Start();

                StopReplyTicker();
                return;
            }

            if (_waitingReplyTicks > _waitingAnswerTime)
            {
                await _gameChannel.SendMessage("No answers were given, resettings Question Time");

                ResetQuestionTime();
                return;
            }

            ++_waitingReplyTicks;
            await _messageWaiting.Edit($"Round will start in {_waitingAnswerTime - _waitingReplyTicks} seconds.");
        }

        private static void StopReplyTicker()
        {
            _waitingReplyTimer.Stop();
            _waitingReplyTimer = null;
        }

        /// <summary>
        /// Called everytime the waiting vote timer ticks.
        /// </summary>
        public static async void AwaitingVoteTick(object obj, ElapsedEventArgs args)
        {
            if (_waitingVoteTicks > _waitingVoteTime)
            {
                int voteACount = 0;
                int voteBCount = 0;

                // add artifical players
                if (_aiCount != 0)
                {
                    for (int i = 0; i < _aiCount; ++i)
                    {
                        //bool voteA = (MathHelpers.GenerateRandomNumber(100) > 25 && MathHelpers.GenerateRandomNumber(125) < 80 && MathHelpers.GenerateRandomNumber(150) > 60);
                        bool voteA = MathHelpers.GenerateRandomNumber(100) > 50;
                        QtVote newVote = new QtVote {VoteCast = (QuestionTimeVoteType) (voteA ? 0 : 1)};
                        CurrentVotes.Add(newVote);
                    }
                }

                for (int i = 0; i < CurrentVotes.Count; ++i)
                {
                    if (CurrentVotes[i].VoteCast == QuestionTimeVoteType.A) ++voteACount;
                    if (CurrentVotes[i].VoteCast == QuestionTimeVoteType.B) ++voteBCount;
                }

                if (voteACount == voteBCount)
                {
                    await _gameChannel.SendTTSMessage($"{voteACount} people voted for {_answerOne}, {voteBCount} people voted for {_answerTwo}. It's a tie.");
                } else if (voteACount > voteBCount)
                {
                    await _gameChannel.SendTTSMessage($"{voteACount} people voted for {_answerOne}, {voteBCount} people voted for {_answerTwo}. {_answerOne} wins.");

                } else if (voteBCount > voteACount)
                {
                    await _gameChannel.SendTTSMessage($"{voteACount} people voted for {_answerOne}, {voteBCount} people voted for {_answerTwo}. {_answerTwo} wins.");
                }

                await _messageWaiting.Delete();
                ResetQuestionTime();

                return;
            }

            ++_waitingVoteTicks;
            if (_messageWaiting != null) await _messageWaiting.Edit($"Round will end in {_waitingVoteTime - _waitingVoteTicks} seconds.");
        }

        private static void StopVoteTicker()
        {
            _waitingVoteTimer.Stop();
            _waitingVoteTimer = null;
        }

        private static void MessageRecieved(object sender, MessageEventArgs e)
        {
            if (e.Channel == _answerOneChannel || e.Channel == _answerTwoChannel)
            {
                string message = e.Message.Text;
                if (message.StartsWith($"@{Program.Client.CurrentUser.Name} qanswer "))
                {
                    message = message.Replace($"@{Program.Client.CurrentUser.Name} qanswer ", "");
                    if (e.Channel == _answerOneChannel)
                    {
                        _answerOneReplied = true;
                        _answerOne = message;
                    }
                    else
                    {
                        _answerTwoReplied = true;
                        _answerTwo = message;
                    }

                    e.Channel.SendMessage("Thank you for answering.");
                }
            }
        }

        /// <summary>
        /// Stops question time and restores it to the default state.
        /// </summary>
        public static void ResetQuestionTime()
        {
            CurrentState = QuestionTimeState.Inactive;
            CurrentUsers.Clear();
            CurrentVotes.Clear();
            _waitingPlayerTicks = 0;
            _waitingReplyTicks = 0;
            _waitingVoteTicks = 0;
            _waitingPlayerTimer?.Stop();
            _waitingPlayerTimer = null;
            _waitingReplyTimer?.Stop();
            _waitingReplyTimer = null;
            _waitingVoteTimer?.Stop();
            _waitingVoteTimer = null;
            _gameChannel = null;
            _answerOneChannel = null;
            _answerTwoChannel = null;
            _messageWaiting = null;
            _usingBot = false;
            _answerOneReplied = false;
            _answerTwoReplied = false;
            _answerOne = "";
            _answerTwo = "";

            Program.Client.MessageReceived -= MessageRecieved;
        }

        public enum QuestionTimeState
        {
            Inactive,
            AwaitingPeople,
            AwaitingQuestionAnswers,
            AwaitingVote,
            AnnouncingWinner
        }
    }

    public class QtVote
    {
        public User VoteUser;
        public QuestionTimeVoteType VoteCast;
    }

    public enum QuestionTimeVoteType
    {
        A,
        B
    }
}
