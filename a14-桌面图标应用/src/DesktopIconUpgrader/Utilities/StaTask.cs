namespace DesktopIconUpgrader.Utilities;

public static class StaTask
{
    public static Task Run(Action action, CancellationToken cancellationToken = default) =>
        Run(() =>
        {
            action();
            return true;
        }, cancellationToken);

    public static Task<T> Run<T>(Func<T> action, CancellationToken cancellationToken = default)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                completion.SetResult(action());
            }
            catch (OperationCanceledException)
            {
                completion.SetCanceled(cancellationToken);
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        })
        {
            IsBackground = true,
            Name = "DesktopIconWorker"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }
}
