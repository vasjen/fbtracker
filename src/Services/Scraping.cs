namespace fbtracker.Services{
    public static class Scraping{

        public static async Task<string[]> GetPageAsStrings(HttpClient client, string url)
        {
            string res = await client.GetStringAsync(url);
            return res.Split(Environment.NewLine);
        }
    }
}