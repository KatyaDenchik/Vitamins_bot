using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace VitaminStoreBot
{
    public class BotConfig
    {
        public string ConfigFilePath { get; init; }

        public event Action TokenChanged;
        private string token;
        public string Token 
        {  get => token;
           set 
           {  token = value;
              IniConfig.Write("Bot", "Token", token);
              TokenChanged?.Invoke();
            } 
        }

        public string SecretWord { get; init; }

        public string PathToExecutableFolder { get; init; }
        public IniParser IniConfig { get; init; }
        public Dictionary<long, (string FirstName, string LastName)> Admins { get; private set; }

        public BotConfig()
        {
            PathToExecutableFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            ConfigFilePath = Path.Combine(PathToExecutableFolder, "Config.ini");
            IniConfig = new IniParser(ConfigFilePath);
            token = IniConfig.Read("Bot", "Token", "7364374816:AAFLeLjDxyjSMwoCObtOWfr8FHlgK08JNg8");
            SecretWord = IniConfig.Read("Bot", "SecretWord", "gyr3061hebz]7wv01r");

            LoadAdmins();
        }

        private void LoadAdmins()
        {
            Admins = new Dictionary<long, (string FirstName, string LastName)>();
            var adminSection = IniConfig.GetSection("Admins");
            foreach (var key in adminSection.Keys)
            {
                var parts = adminSection[key].Split(':');
                if (parts.Length == 3 && long.TryParse(parts[0], out long chatId))
                {
                    Admins[chatId] = (parts[1], parts[2]);
                }
            }
        }

        public void SaveAdmin(long chatId, string firstName, string lastName)
        {
            Admins[chatId] = (firstName, lastName);
            IniConfig.Write("Admins", chatId.ToString(), $"{chatId}:{firstName}:{lastName}");
        }
    }
}
