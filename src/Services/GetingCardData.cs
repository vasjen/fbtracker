using fbtracker.Models;
using fbtracker.Services.Interfaces;
using Newtonsoft.Json;

namespace fbtracker.Services
{

    public class GetingCardData : IGetingCardData{
        private readonly ILogger<GetingCardData> _logger;
        private readonly IImageService _imageService;
        private const string URL = "https://www.futbin.com/24/getPlayerSales?platform=ps&resourceId=";
        private IEnumerable<SalesHistory>? Histories { get; set; }

        public GetingCardData(ILogger<GetingCardData> logger, IImageService imageService)
        {
            _logger = logger;
            _imageService = imageService;
        }
       
        public async Task GetPriceAsync(Card card, HttpClient client)
        {
            if (card.FbDataId == 0) 
                Scraping.GetDataId(card,client);
            
            _logger.LogInformation("Check price for {0}", card.ToString());
            string requestUri = $"https://futbin.com/24/playerPrices?player={card.FbDataId}";
            await Task.Delay(1000);
            try
            {
                HttpResponseMessage response = await client.GetAsync(requestUri);
                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    card.Prices = Scraping.GetPrices(card.FbDataId, jsonResponse);
                }
                else
                {
                    _logger.LogInformation("Can't get a price for {0}", card.ToString());
                    _logger.LogInformation("Reason: {0}", response.ReasonPhrase);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
            }

        }

        public async Task<IEnumerable<SalesHistory>?> GetSalesHistoryAsync(int fbDataId, HttpClient client)
        {
            
            string sales = await client.GetStringAsync(URL+fbDataId);
            try 
            {
                this.Histories = JsonConvert.DeserializeObject<IEnumerable<SalesHistory>>(sales);
            }
            catch (JsonException ex)
            {
                _logger.LogInformation(ex.Message);
                _logger.LogError("Can't deserialize sales history for player with id {0}",fbDataId);
            }
            return this.Histories;
        }

        public  void AddImagesUrlToCard(Card card)
        {
            card.PromoUrl =  Scraping.GetBackgroundImage(Scraping.URL + "/player/" + card.FbId);
            string link = card.PromoUrl.Substring(card.PromoUrl.LastIndexOf('/') + 1);
            card.PromoUrlFile = link.Remove(link.IndexOf('?'));
            
            if ((card.PromoUrl.Length == 0 || card.PromoUrlFile is null))
                throw new Exception("Can't get image url");
        }

        public async Task DownloadImageAsync(string directoryPath, string fileName, Uri uri)
        {
            await _imageService.DownloadImageAsync(directoryPath,fileName, uri);
        }

        public async Task CombineImages(string firstFilePath, string secondFilePath, string outputFilePath)
        {
            await _imageService.CombineImages(firstFilePath, secondFilePath, outputFilePath);
        }
    }
}