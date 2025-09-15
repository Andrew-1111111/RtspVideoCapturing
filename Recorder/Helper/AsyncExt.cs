using System.Runtime.CompilerServices;

namespace RtspVideoCapturing.Helper
{
    internal class AsyncExt
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static async Task<TResult> TimeoutAsync<TResult>(Task<TResult> task, TimeSpan timeout)
        {
            using var timeoutCTS = new CancellationTokenSource();
            var completedTask = await Task.WhenAny(task, Task.Delay(timeout, timeoutCTS.Token));
            if (completedTask == task)
            {
                timeoutCTS.Cancel();
                return await task;  // Very important in order to propagate exceptions
            }
            else
            {
                throw new TimeoutException("The operation has timed out");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static async Task TimeoutAsync(Task? task, TimeSpan timeout)
        {
            ArgumentNullException.ThrowIfNull(task);

            using var timeoutCTS = new CancellationTokenSource();
            var completedTask = await Task.WhenAny(task, Task.Delay(timeout, timeoutCTS.Token));
            if (completedTask == task)
            {
                timeoutCTS.Cancel();
                await task;  // Very important in order to propagate exceptions
            }
            else
            {
                throw new TimeoutException("The operation has timed out");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static async Task TimeoutAsync(Task? task, int timeoutMs)
        {
            ArgumentNullException.ThrowIfNull(task);
            
            using var timeoutCTS = new CancellationTokenSource();
            var completedTask = await Task.WhenAny(task, Task.Delay(timeoutMs, timeoutCTS.Token));
            if (completedTask == task)
            {
                timeoutCTS.Cancel();
                await task;  // Very important in order to propagate exceptions
            }
            else
            {
                throw new TimeoutException("The operation has timed out");
            }
        }
    }
}