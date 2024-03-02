using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace fbtracker.Services;

public abstract class BackgroundWorkerService : BackgroundService, IHealthCheck
{
    
    public TaskStatus DoWorkStatus { get; set; } = TaskStatus.Created;
    public abstract int ExecutionInterval { get; }
    
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (this.DoWorkStatus is not TaskStatus.Running)
                {
                    
                    this.DoWorkStatus = TaskStatus.Running;
                    await DoWork(cancellationToken);
                    this.DoWorkStatus = TaskStatus.RanToCompletion;
                }
                await Task.Delay(this.ExecutionInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                this.DoWorkStatus = TaskStatus.Canceled;
                
                return;
            }
            catch (Exception e)
            {
                this.DoWorkStatus = TaskStatus.Faulted;
                
                await Task.Delay(this.ExecutionInterval, cancellationToken);
            }
        }
    }
    protected abstract Task DoWork(CancellationToken cancellationToken);
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = new CancellationToken())
    {
        try
        {
            if (this.DoWorkStatus is TaskStatus.Running)
            {
                return HealthCheckResult.Healthy();
            }
            else
            {
                return HealthCheckResult.Degraded();
            }
        }
        catch (Exception e)
        {
            
            return HealthCheckResult.Unhealthy();
        }
    }
}