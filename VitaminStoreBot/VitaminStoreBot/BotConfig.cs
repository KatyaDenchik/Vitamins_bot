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

        public string PathToExecutableFolder { get; init; }
        public IniParser IniConfig { get; init; }

        public BotConfig()
        {
            PathToExecutableFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            ConfigFilePath = Path.Combine(PathToExecutableFolder, "Config.ini");
            IniConfig = new IniParser(ConfigFilePath);
            token = IniConfig.Read("Bot", "Token", "7364374816:AAFLeLjDxyjSMwoCObtOWfr8FHlgK08JNg8");
        }
    }
}
