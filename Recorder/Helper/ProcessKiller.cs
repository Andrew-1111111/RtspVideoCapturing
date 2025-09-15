using System.Diagnostics;

namespace RtspVideoCapturing.Helper
{
    /// <summary>
    /// Класс для завершения процессов
    /// </summary>
    internal static class ProcessKiller
    {
        /// <summary>
        /// Завершает процесс FFMpeg
        /// </summary>
        internal static void Kill_FFMpeg()
        {
            using var killFfmpeg = new Process();
            killFfmpeg.StartInfo = new ProcessStartInfo
            {
                FileName = "taskkill",
                // Флаг /F - принудительное завершение процесса
                // Флаг /IM образ - имя образа процесса, который требуется завершить
                Arguments = "/F /IM ffmpeg.exe", 
                UseShellExecute = false,
                CreateNoWindow = true
            };
            killFfmpeg.Start();
        }
    }
}