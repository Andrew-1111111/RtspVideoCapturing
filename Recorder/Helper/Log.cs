using System.Runtime.CompilerServices;

namespace RtspVideoCapturing.Helper
{
    /// <summary>
    /// Логирование строк в текстовые файлы (не используется, оставил для возможного логирования и отладки)
    /// </summary>
    internal static class Log
    {
        private static readonly SemaphoreSlim _semaphoreSlim = new(1, 1);

        internal static bool WriteExceptionLine(string line, [CallerMemberName]string caller = "")
        {
            try
            {
                // Synchronus wait enter in SemaphoreSlim critical section
                _semaphoreSlim.Wait();

                using var sw = File.AppendText(AppDomain.CurrentDomain.BaseDirectory + "ExLog.txt");
                sw.WriteLine($"[{DateTime.Now}] Caller: " + caller);
                sw.WriteLine(line);
                sw.WriteLine("-------------------------------------------");
                sw.Flush();
                return true;
            }
            catch { }
            finally
            {
                // Exit from critical section
                _semaphoreSlim.Release();
            }

            return false;
        }

        internal static async Task<bool> WriteExceptionLineAsync(string line, [CallerMemberName]string caller = "")
        {
            try
            {
                // Asynchronus wait enter in SemaphoreSlim critical section
                await _semaphoreSlim.WaitAsync();

                using var sw = File.AppendText(AppDomain.CurrentDomain.BaseDirectory + "ExLog.txt");
                await sw.WriteLineAsync($"[{DateTime.Now}] Caller: " + caller);
                await sw.WriteLineAsync(line);
                await sw.WriteLineAsync("-------------------------------------------");
                await sw.FlushAsync();
                return true;
            }
            catch { }
            finally
            {
                // Exit from critical section
                _semaphoreSlim.Release();
            }

            return false;
        }
    }
}