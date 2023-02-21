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
          
           
            if (!context.Cards.Any()) {
           
                var cards = await initial.GetCardsRangeAsync();
                await context.AddRangeAsync(cards);
                await context.SaveChangesAsync();
                System.Console.WriteLine("List of cards was added");
            }


        }
    }
}
