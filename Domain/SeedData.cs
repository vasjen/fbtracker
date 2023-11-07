using fbtracker.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace fbtracker {

    public static class SeedData {

        public static async IAsyncEnumerable<Card> EnsurePopulatedAsync(IHost host) {
            FbDbContext context = host.Services
                .CreateScope().ServiceProvider.GetRequiredService<FbDbContext>();
            var initial = host.Services.GetRequiredService<IInitialService>();
            
            if (context.Database.GetPendingMigrations().Any()) {
                context.Database.Migrate();
            }

            using (IServiceScope scope = host.Services.CreateScope())
            {
                IWebService webService =
                    scope.ServiceProvider
                        .GetRequiredService<IWebService>();
                List<HttpClient> clients =
                    await webService.CreateHttpClients(webService.CreateHandlers(webService.GetProxyList()));
                int currentIndex = 0;

                HttpClient GetNextClient()
                {
                    HttpClient client = clients[currentIndex];
                    client.DefaultRequestHeaders
                        .Add("User-Agent","User Agent	Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko)");
                    currentIndex = (currentIndex + 1) % clients.Count;
                    return client;
                }
                if (!context.Cards.Any())
                {
              
                

                    var numbers = await initial.GetMaxNumberPage("https://www.futbin.com/players?player_rating=80-99&ps_price=10000-15000000");
                    for (int i = 1; i <= numbers; i++)
                    {
                        var client = GetNextClient();
                        var cards =  initial.GetCards(
                            $"https://www.futbin.com/players?page={i}&player_rating=80-99&ps_price=10000-15000000", client);
                        await foreach (var item in cards)
                        {
                            Console.WriteLine("Name: {0}, Version: {1}, Position: {2}, Rating {3}",item.ShortName,item.Version, item.Position, item.Raiting);
                            yield return item;
                        }
                    
                    }
                
                    // var cards =  initial.GetCards();
                    // await context.AddRangeAsync(cards);
                    // await context.SaveChangesAsync();
                    // System.Console.WriteLine("List of cards was added");
                }
                
            }

            


        }
    }
}
