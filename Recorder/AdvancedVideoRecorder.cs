using System.Diagnostics;

namespace RtspVideoCapturing.Recorder
{
    /// <summary>
    /// Поддерживает запись нескольких потоков с камер
    /// </summary>
    internal class AdvancedVideoRecorder
    {
        private readonly List<(string, string)> _cams;
        private readonly string _tempDir;
        private readonly string _recordsDir;

        // Глобальный CTS
        private readonly CancellationTokenSource _globalCts;

        /// <summary>
        /// Основной конструктор
        /// </summary>
        /// <param name="cams">Список с камерами, вида: "CamName", "rtsp://login:password@ip:port/"</param>
        /// <param name="tempDir">Директория для временных файлов</param>
        /// <param name="recordsDir">Директория для видеозаписей</param>
        /// <param name="globalCts">Сигнализирует "CancellationToken" о том, что его следует отменить</param>
        /// <exception cref="ArgumentException">Аргумент cams не должен быть пустым</exception>
        /// <exception cref="DirectoryNotFoundException">Директории tempDir, recordsDir должны существовать</exception>
        internal AdvancedVideoRecorder(List<(string, string)> cams, string tempDir, string recordsDir, CancellationTokenSource globalCts)
        {
            if (cams == null || cams.Count < 1)
                throw new ArgumentException("The cams must be not empty!");

            if (!Directory.Exists(tempDir))
                throw new DirectoryNotFoundException("The tempDir directory must be exists!");

            if (!Directory.Exists(recordsDir))
                throw new DirectoryNotFoundException("The recordsDir directory must be exists!");

            _cams = cams;
            _tempDir = tempDir;
            _recordsDir = recordsDir;
            _globalCts = globalCts;
        }

        /// <summary>
        /// Запуск захвата видеопотока с сохранением в файлы (+ детектор движения) для нескольких камер
        /// </summary>
        /// <returns>Асинхронная задача</returns>
        internal async Task RunRecordsAsync()
        {
            var tasks = new Task[_cams.Count];

            for (int i = 0; i < tasks.Length; i++)
            {
                var cam = _cams[i];

                // Проверка инстансов кортежа
                if (!string.IsNullOrWhiteSpace(cam.Item1) && !string.IsNullOrWhiteSpace(cam.Item2))
                {
                    // Инициализируем каждую задачу
                    tasks[i] = Task.Run(async () =>
                    {
                        // Инициализируем и заполняем параметры класса-рекордера
                        var vRecorder = new VideoRecorder(
                            cam.Item1,
                            cam.Item2,
                            _tempDir,
                            _recordsDir,
                            VideoRecorder.MotionDifferenceMethod.OpenCV, // OpenCV режим детектора движения
                            _globalCts);

                        try
                        {
                            // Запускаем первичный метод получения rtsp потока с детектором движения
                            if (await vRecorder.StartMonitoringAsync())
                            {
                                // Запускаем основной метод получения rtsp потока с записью видео в файлы и детектором движения
                                await vRecorder.StartRecordingAsync();
                            }
                        }
                        catch (OperationCanceledException)
                        {
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(ex.Message);
                        }
                    },
                    _globalCts.Token);
                }
            }

            // Запускаем и ожидаем завершение всех задач
            await Task.WhenAll(tasks.Where(t => t != null));
        }

        /// <summary>
        /// Глобально останавливает запись текущего экземпляра класса
        /// </summary>
        internal void StopRecording()
        {
            // Отправляем запрос на отмену в CTS
            if (_globalCts != null && !_globalCts.IsCancellationRequested)
            {
                _globalCts.Cancel();
            }

            _globalCts?.Dispose();
        }
    }
}