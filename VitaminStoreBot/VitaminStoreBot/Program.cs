namespace VitaminStoreBot
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var config = new BotConfig();
            var bot = new Bot(config);
            bot.Start();
        }
    }
}
