
using fbtracker.Models;
using fbtracker.Services.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
namespace fbtracker.Services {
    public class TelegramService : ITelegramService
    {
        private readonly ITelegramBotClient _client;
        private readonly string _chatId;
        private const string URL = "https://www.futbin.com/player/";

        public TelegramService(ITelegramBotClient client, IConfiguration config) 
        {
            _client = client;
            _chatId = config.GetSection("Telegram").GetValue<string>("ChatId"); 
        }

       
        public async Task SendInfo(Profit profitPlayer, int avgPrice, IEnumerable<SalesHistory> lastTenSales, Card card)
        {
            string notification = await CreateNotificationAsync(profitPlayer, avgPrice, lastTenSales, card);
            await _client.SendTextMessageAsync(
                _chatId, notification, 
                ParseMode.Html ,disableWebPagePreview: true,  allowSendingWithoutReply: true );
        }

         async Task<string> CreateNotificationAsync(Profit profitPlayer, int avgPrice, IEnumerable<SalesHistory> lastSales, Card card)
         { 
             string profitMessage = 
                                 $"<a href=\"{URL}{card.FbId}\">{card.ShortName} {card.Version} "+
                                 $"{card.Rating} {card.Position}</a>" + 
                                 $"\n \n<u>New price:</u> <b>{profitPlayer.Price:0,0}</b> &#128176  \n<u>Profit:"+
                                 $"</u><b>{profitPlayer.ProfitValue:0,0}</b> &#128200 \n<u>Old price:</u> "+
                                 $"<b>{profitPlayer.SellPrice:0,0}</b> &#128176 \n"+
                                 $"\n<u>Change:</u> <b>- {1 - (profitPlayer.Percentage):0.00%}</b> &#128315 \n \n ";
            string historyMessage = 
                $" \n<u>Average:</u> <b>{avgPrice:0,0}</b>  &#128176 \n\n"+
                $"&#9201 List of the last ten sales: (UTC±0:00) &#9201\n  \n";
            List<SalesHistory> sales = lastSales.Take(10).ToList();
            
            historyMessage = sales.Aggregate(historyMessage, (current, t) => current + "<i>" + t.Price.ToString("0,0") + " in " + t.updated + " </i>\n");
            return profitMessage + historyMessage;    
        }
    }
}