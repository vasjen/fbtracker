
using fbtracker.Models;
using fbtracker.Services.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InputFiles;

namespace fbtracker.Services {
    public class TelegramService : INotificationService
    {
        private readonly ITelegramBotClient _client;
        private readonly string? _chatId;
        private readonly IImageService _imageService;
        private readonly ILogger<TelegramService> _logger;
        private const string URL = "https://www.futbin.com/player/";

        public TelegramService(ITelegramBotClient client, IConfiguration config, IImageService imageService, ILogger<TelegramService> logger) 
        {
            _client = client;
            _chatId = config.GetSection("Telegram").GetValue<string>("ChatId");
            _imageService = imageService;
            _logger = logger;
        }

       
        // public async Task SendInfo(ProfitCard profitCardPlayer, int avgPrice, IEnumerable<SalesHistory> lastTenSales, Card card)
        // {
            
            // HttpClient client = new HttpClient();
            // client.DefaultRequestHeaders.Add("User-Agent", "User Agent	Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko)");
            // try
            // {
                // _imageService.DownloadImageAsync("Cards",$"{card.FbDataId}.png",new Uri(card.ImageUrl)).GetAwaiter().GetResult();
                // _imageService.DownloadImageAsync("Promo",$"{card.PromoUrlFile}",new Uri(card.PromoUrl)).GetAwaiter().GetResult();
                // await _imageService.CombineImages("Cards/" + $"{card.FbDataId}.png","Promo/" + $"{card.PromoUrlFile}",$"{card.FbDataId}.png");
            // }
            // catch (Exception e)
            // {
                // _logger.LogError(e.Message);
            // }
           
            // string notification = await CreateNotificationAsync(profitCardPlayer, avgPrice, lastTenSales, card);
            
            
            
            // await using FileStream imageStream = File.OpenRead($"{card.FbDataId}.png");
            
           // await _client.SendPhotoAsync(
                // _chatId,new InputOnlineFile(imageStream, "result.jpg"), notification, 
                // ParseMode.Html ,allowSendingWithoutReply: true );
        // }

         async Task<string> CreateNotificationAsync(ProfitCard profitCardPlayer)
         { 
             
             string profitMessage = 
                                 $"<a href=\"{URL}{profitCardPlayer.Card.FbId}\">{profitCardPlayer.Card.ShortName} {profitCardPlayer.Card.Version} "+
                                 $"{profitCardPlayer.Card.Rating} {profitCardPlayer.Card.Position}</a>" + 
                                 $"\n \n<u>New price:</u> <b>{profitCardPlayer.Price:0,0}</b> &#128176  \n<u>Profit:"+
                                 $"</u><b>{profitCardPlayer.ProfitValue:0,0}</b> &#128200 \n"+
                                 $"\n<u>Change:</u> <b>- {  (profitCardPlayer.Percentage):0.00%}</b> &#128315 \n" +
                                 $"<u>Updated:</u> <b>{profitCardPlayer.Card.Prices.Ps.Updated}</b>\n \n ";
            string historyMessage = 
                $" \n<u>Market price:</u> <b>{profitCardPlayer.SellPrice:0,0}</b>  &#128176 \n\n"+
                $"Last ten sales: (UTC±0:00) &#9201\n  \n<pre>";
            List<SalesHistory> sales = profitCardPlayer.LastSales.Take(10).ToList();
            
            historyMessage = sales.Aggregate(historyMessage, (current, t) => current + "<i>" + t.Price.ToString("0,0") + " in " + t.updated + " </i>\n");
            return profitMessage + historyMessage + "\n</pre>";    
        }

         public async Task SendMessageAsync(ProfitCard profitCardPlayer)
         {
             string message = await CreateNotificationAsync(profitCardPlayer);
             await using FileStream imageStream = File.OpenRead($"{profitCardPlayer.Card.FbDataId}.png");
             await _client.SendPhotoAsync(
                 _chatId,new InputOnlineFile(imageStream, "result.jpg"), message, 
                 ParseMode.Html ,allowSendingWithoutReply: true );
             _logger.LogInformation($"Message with card: {profitCardPlayer.Card} sent to telegram");
         }
    }
}