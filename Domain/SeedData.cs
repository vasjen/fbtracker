using Microsoft.EntityFrameworkCore;

namespace fbtracker {

    public static class SeedData {

        public static async Task EnsurePopulatedAsync(IHost host) {
            FbDbContext context = host.Services
                .CreateScope().ServiceProvider.GetRequiredService<FbDbContext>();
            var initial = host.Services.GetRequiredService<IInitialService>();
            
            if (context.Database.GetPendingMigrations().Any()) {
                context.Database.Migrate();
            }
          
           
            if (!context.Cards.Any())
            {

                var numbers = await initial.GetMaxNumberPage("https://www.futbin.com/players?player_rating=82-99&ps_price=20000-15000000");
                for (int i = 1; i <= numbers; i++)
                {
                    var cards =  initial.GetCards(
                        $"https://www.futbin.com/players?page={i}&player_rating=82-99&ps_price=20000-15000000");
                    await foreach (var item in cards)
                    {
                        Console.WriteLine(item.ShortName,item.Price);
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
