using System.Diagnostics;
using fbtracker.Models;
using fbtracker.Services.Interfaces;

namespace fbtracker.Services;

public class PriceCheckerBackground : BackgroundWorkerService
{
    private readonly ILogger<PriceCheckerBackground> _logger;
    private readonly IServiceProvider _services;
    private readonly SeedData _seedData;
    private readonly IProfitService _profitService;

    public PriceCheckerBackground(ILogger<PriceCheckerBackground> logger, IServiceProvider services, SeedData seedData, IProfitService profitService) 
    {
        _logger = logger;
        _services = services;
        _seedData = seedData;
        _profitService = profitService;
    }


    public override int ExecutionInterval => Convert.ToInt32(TimeSpan.FromSeconds(10).TotalMilliseconds);

    protected override async Task DoWork(CancellationToken cancellationToken)
    {
            _logger.LogInformation("PriceCheckerBackground Hosted Service is working. Started at {0}", DateTime.Now); 
            Stopwatch timer = new();
            timer.Start();
            
            IAsyncEnumerable<Card> cards =  _seedData.EnsurePopulatedAsync(_services);
            await _profitService.FindProfitCards(cards);
        
            _logger.LogInformation(
                "PriceCheckerBackground Hosted Service is finished at {0}.\n Total time: {1}", DateTime.Now, timer.Elapsed);
            timer.Reset();
    }
}

    