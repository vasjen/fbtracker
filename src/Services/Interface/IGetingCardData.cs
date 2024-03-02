using fbtracker.Models;

namespace fbtracker.Services.Interfaces
{
    public interface IGetingCardData
    {
        Task GetPriceAsync (Card card, HttpClient client);
         
         public Task<IEnumerable<SalesHistory>?> GetSalesHistoryAsync (int fbDataId, HttpClient client);
         
         public void AddImagesUrlToCard(Card card);
         
         Task DownloadImageAsync(string directoryPath, string fileName, Uri uri);
         Task CombineImages(string firstFilePath, string secondFilePath, string outputFilePath); 
    }
}