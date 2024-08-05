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
        public List<long> Admins { get; private set; } = new();

        public BotConfig()
        {
            PathToExecutableFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            ConfigFilePath = Path.Combine(PathToExecutableFolder, "Config.ini");
            IniConfig = new IniParser(ConfigFilePath);
            token = IniConfig.Read("Token", defultValue: "7364374816:AAFLeLjDxyjSMwoCObtOWfr8FHlgK08JNg8");
            SecretWord = IniConfig.Read("SecretWord", defultValue: "gyr3061hebz]7wv01r");

            LoadAdmins();
        }

        private void LoadAdmins()
        {
            var admins = IniConfig.Read("Admins");
            foreach (var admin in admins.Replace(" ", "").Split(';'))
            {
                if (string.IsNullOrEmpty(admin))
                {
                    continue;
                }
                Admins.Add(long.Parse(admin));
            }
        }

        public void SaveAdmin(long chatId)
        {
            Admins.Add(chatId);
            var existingAdmins = IniConfig.Read("Admins");

            IniConfig.Write("Admins", $"{chatId}; {existingAdmins}");
        }
    }
}
