using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VitaminStoreBot
{
    public class BotService : BackgroundService
    {
        private Task workingTask;
        public BotService()
        {
            var config = new BotConfig();
            var bot = new Bot(config);
            bot.Start();
        }
        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            workingTask = Task.Run(() => {
                
                Console.ReadLine();
            });
            return workingTask;
        }
    }
}
