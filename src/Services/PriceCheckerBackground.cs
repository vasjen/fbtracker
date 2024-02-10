using System.Diagnostics;
using fbtracker.Models;
using fbtracker.Services.Interfaces;

namespace fbtracker.Services;

public class PriceCheckerBackground : BackgroundWorkerService
{
    private readonly ILogger<PriceCheckerBackground> _logger;
    private readonly IServiceProvider _services;

    public PriceCheckerBackground(ILogger<PriceCheckerBackground> logger, IServiceProvider services) 
       
    {
        _logger = logger;
        _services = services;
    }


    public override int ExecutionInterval => Convert.ToInt32(TimeSpan.FromSeconds(10).TotalMilliseconds);

    protected override async Task DoWork(CancellationToken cancellationToken)
    {
        
      
        using (IServiceScope scope = this._services.CreateScope())
        {
            _logger.LogInformation("PriceCheckerBackground Hosted Service is working. Started at {0}", DateTime.Now); 
            Stopwatch timer = new();
            timer.Start();
            SeedData? seedData = scope.ServiceProvider.GetService<SeedData>();
            IAsyncEnumerable<Card> cards =  seedData.EnsurePopulatedAsync(_services);
            IProfitService profitService = scope.ServiceProvider.GetService<IProfitService>();
            await profitService.FindProfitCards(cards);
        
            _logger.LogInformation(
                "PriceCheckerBackground Hosted Service is finished at {0}.\n Total time: {1}", DateTime.Now, timer.Elapsed);
            timer.Reset();
        }
        
    }
}

    