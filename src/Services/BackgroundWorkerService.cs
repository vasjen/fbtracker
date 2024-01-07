namespace fbtracker.Services;

public abstract class BackgroundWorkerService : BackgroundService
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
}