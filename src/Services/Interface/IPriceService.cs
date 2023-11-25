namespace fbtracker.Services.Interfaces
{
    public interface IPriceService
    {
         Task<int[]> GetPriceAsync (int FBDataId, HttpClient client);
    }
}