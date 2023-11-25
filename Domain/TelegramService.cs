using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
namespace fbtracker {
    public class TelegramService : ITelegramService
    {
        private readonly ITelegramBotClient _client;
        private readonly FbDbContext _context;
        private readonly IConfiguration _config;
        public readonly string _chatId;
        private const string URL="https://www.futbin.com/player/";

        public TelegramService(ITelegramBotClient client, FbDbContext context, IConfiguration config)
    {
        _client=client;
        _context=context;
        _config=config;
        _chatId=config.GetSection("Telegram").GetValue<string>("ChatId");
    }

       
        public async Task SendInfo(Profit ProfitPlayer, int avgPrice, IEnumerable<SalesHistory> lastTenSales, Card card)
        {
            var Notification = await CreateNotificationAsync(ProfitPlayer, avgPrice, lastTenSales, card);
            await _client.SendTextMessageAsync(_chatId, Notification+$"\n &#11015 Add your deals in comments &#11015", ParseMode.Html ,disableWebPagePreview: true,  allowSendingWithoutReply: true );
                        
        }

         async Task<string> CreateNotificationAsync(Profit ProfitPlayer, int avgPrice, IEnumerable<SalesHistory> lastTenSales, Card Card){
        
            // var Card = await _context.Cards
                        // .AsNoTracking()
                        // .Where(p=>p.CardId==ProfitPlayer.CardId)
                        // .FirstOrDefaultAsync();
            
           

            string ProfiteMessage=$"<a href=\"{URL}{Card.FbId}\">{Card.ShortName} {Card.Version} "+
                $"{Card.Raiting} {Card.Position}</a>" + 
                $"\n \n<u>New price:</u> <b>{ProfitPlayer.Price.ToString("0,0")}</b> &#128176  \n<u>Profit:"+
                $"</u><b>{ProfitPlayer.ProfitValue.ToString("0,0")}</b> &#128176  &#128200 \n<u>Old price:</u> "+
                $"<b>{ProfitPlayer.SellPrice.ToString("0,0")}</b> &#128176 \n"+
                $"\n<u>Change:</u> <b>{ProfitPlayer.Percentage.ToString("0.00%")}</b> &#9889 \n \n ";
            string HistoryMessage = $" \n<u>Average:</u> <b>{avgPrice.ToString("0,0")}</b> &#128202 \n\n"+
                $"&#9201 List of the last five sales: (UTC±0:00) &#9201\n  \n";
            var sales = lastTenSales.Take(5).ToList();
                 

            foreach (var t in sales)
            {
             HistoryMessage=HistoryMessage+"<i>"+t.Price.ToString("0,0")+" in "+t.updated+" </i>\n";
            }
        


            string Notification = ProfiteMessage+HistoryMessage;    
            
            return  Notification;
        }

        
    }
}