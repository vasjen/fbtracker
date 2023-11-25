namespace fbtracker
{
    public interface IPriceService
    {
         Task<int[]> GetPriceAsync (int FBDataId, HttpClient client);
         
    }
}