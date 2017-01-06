using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot
{
    public class Settings
    {
        private static readonly string SettingsFile = Environment.CurrentDirectory + "/settings.ini";

        /// <summary>
        /// The token to use when connecting the bot.
        /// </summary>
        public static string BotToken;
        
        /// <summary>
        /// What the bot is playing
        /// </summary>
        public static string BotGame;

        /// <summary>
        /// Loads the bots settings.
        /// </summary>
        public static void LoadSettings()
        {
            // if the file doesn't exist then ask for default settings then create the ini
            // otherwise load the settings
            if (File.Exists(SettingsFile))
            {
                IniParser ini = new IniParser();
                ini.Open(SettingsFile);

                BotToken = ini.ReadValue("Settings", "Bot", "Enter Token");
                BotGame = ini.ReadValue("Settings", "Game", "");

                ini.Close();

                Console.WriteLine("Settings have been loaded.");
            }
            else
            {
                Console.WriteLine("Enter Bot Token: ");
                BotToken = Console.ReadLine();
                Console.WriteLine("Enter Bot Game: ");
                BotGame = Console.ReadLine();

                IniParser ini = new IniParser();
                ini.Open(SettingsFile);

                ini.WriteValue("Settings", "Bot", BotToken);
                ini.WriteValue("Settings", "Game", BotGame);

                ini.Close();

                Console.WriteLine("Settings have been created");
            }

        }
    }
}
