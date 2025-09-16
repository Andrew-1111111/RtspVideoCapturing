namespace RtspVideoCapturing.Recorder.Helper
{
    internal class AsyncExt
    {
        internal static async Task TimeoutAsync(Task? task, TimeSpan timeout)
        {
            ArgumentNullException.ThrowIfNull(task);

            using var timeoutCTS = new CancellationTokenSource();
            var completedTask = await Task.WhenAny(task, Task.Delay(timeout, timeoutCTS.Token));
            if (completedTask == task)
            {
                timeoutCTS.Cancel();
                await task;  // Очень важно для распространения исключений
            }
            else
            {
                throw new TimeoutException("The operation has timed out");
            }
        }

        internal static async Task<TResult> TimeoutAsync<TResult>(Task<TResult> task, TimeSpan timeout)
        {
            using var timeoutCTS = new CancellationTokenSource();
            var completedTask = await Task.WhenAny(task, Task.Delay(timeout, timeoutCTS.Token));
            if (completedTask == task)
            {
                timeoutCTS.Cancel();
                return await task;  // Очень важно для распространения исключений
            }
            else
            {
                throw new TimeoutException("The operation has timed out");
            }
        }
    }
}