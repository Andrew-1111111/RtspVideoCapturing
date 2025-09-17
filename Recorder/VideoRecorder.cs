using FFMpegCore;
using FFMpegCore.Enums;
using RtspVideoCapturing.Recorder.Helper;
using RtspVideoCapturing.Recorder.Image;
using RtspVideoCapturing.Recorder.OpenCV;
using System.Diagnostics;

namespace RtspVideoCapturing.Recorder
{
    /// <summary>
    /// Класс для записи видеофайлов в формате mp4 из rtsp видеопотока с детектором движения
    /// </summary>
    internal class VideoRecorder
    {
        // Путь до бинарных файлов ffmpeg и ffprobe
        private static readonly string _ffmpegPath = AppDomain.CurrentDomain.BaseDirectory + @"ffmpeg\bin\";

        // Используйте только ".png" (при смене расширения на ".bmp" библиотека ffmpeg ведет себя некорректно, не освобождает файл с изображением)
        private const string IMG_EXTENSION = ".png";

        // Расширение MP4
        private const string VIDEO_EXTENSION = ".mp4";

        // Глобальный и временный CTS
        private readonly CancellationTokenSource _globalCts;
        private CancellationTokenSource _tempCts = null!;

        private readonly MotionDifferenceMethod _motionDifMethod;

        private readonly string _camName;
        private readonly string _rtspUrl;
        private readonly string _tempDir;
        private readonly string _recordsDir;
        private readonly int _loopDelayS;
        private readonly string _etalonImgGuid;
        private readonly string _newImgGuid;
        private readonly string _videoGuid;
        private bool _oldTempImgSet;
        private bool _hasMotion;
        private bool _hasFlushedVideo;

        // Детект сетевых сбоев
        private ulong _etalonFrameTime;
        private ulong _lastFrameTime;

        // Перечисление, для выбора способа определения движения
        [Flags]
        internal enum MotionDifferenceMethod : byte
        {
            Pixel = 0x01,
            OpenCV = 0x02
        }

        /// <summary>
        /// Статический конструктор, вызывается только один раз (настраивает путь к ffmpeg и ffprobe)
        /// </summary>
        static VideoRecorder()
        {
            // Настройка путей к ffmpeg и ffprobe (если они отсутствуют в PATH)
            GlobalFFOptions.Configure(options => options.BinaryFolder = _ffmpegPath);
        }

        /// <summary>
        /// Основной конструктор
        /// </summary>
        /// <param name="camName">Имя камеры</param>
        /// <param name="rtspUrl">Строковой Uri вида: rtsp://login:password@ip:port/</param>
        /// <param name="tempDir">Директория для временных файлов</param>
        /// <param name="recordsDir">Директория для видеозаписей</param>
        /// <param name="motionDifMethod">Выбор метода детектирования (Pixel или OpenCV)</param>
        /// <param name="globalCts">Сигнализирует "CancellationToken" о том, что его следует отменить</param>
        /// <param name="loopDelayS">Задержка в секундах, между итерациями циклов</param>
        /// <exception cref="ArgumentException">Аргументы camName, rtspUrl не должны быть пустыми</exception>
        /// <exception cref="DirectoryNotFoundException">Директории tempDir, recordsDir должны существовать</exception>
        internal VideoRecorder(string camName, string rtspUrl, string tempDir, string recordsDir, MotionDifferenceMethod motionDifMethod,
            CancellationTokenSource globalCts, int loopDelayS = 5)
        {
            if (string.IsNullOrWhiteSpace(camName))
                throw new ArgumentException("The camName must be not empty!");

            if (string.IsNullOrWhiteSpace(rtspUrl))
                throw new ArgumentException("The rtspUrl must be not empty!");

            if (!Directory.Exists(tempDir))
                throw new DirectoryNotFoundException("The tempDir directory must be exists!");

            if (!Directory.Exists(recordsDir))
                throw new DirectoryNotFoundException("The recordsDir directory must be exists!");

            // Задаем директории папок для этой камеры
            _tempDir = tempDir + camName + @"\";
            _recordsDir = recordsDir + camName + @"\";

            // Если не существуют директории для конкретной камеры, создаем их
            if (!Directory.Exists(_tempDir))
                Directory.CreateDirectory(_tempDir);

            if (!Directory.Exists(_recordsDir))
                Directory.CreateDirectory(_recordsDir);

            _camName = camName;
            _rtspUrl = rtspUrl;
            _motionDifMethod = motionDifMethod;
            _globalCts = globalCts;
            _loopDelayS = loopDelayS;

            _etalonImgGuid = "Etalon_" + Guid.NewGuid().ToString();
            _newImgGuid = "New_" + Guid.NewGuid().ToString();
            _videoGuid = "Video_" + Guid.NewGuid().ToString();
            _oldTempImgSet = false;
            _hasMotion = false;
            _hasFlushedVideo = false;
            _etalonFrameTime = 0;
            _lastFrameTime = 0;
        }

        /// <summary>
        /// Первичный метод получения rtsp потока, его задача определить движение и завершиться успешно, чтобы начать запись
        /// </summary>
        /// <param name="waitTimeoutMin">Таймаут, по истечению которого метод завершается (чтобы не ждать бесконечно), 
        /// если значение 0 - таймаут отключен</param>
        /// <param name="videoSegmentS">Длительность первоначального сегмента видео в секундах</param>
        /// <returns>True - есть движение, False - нет движения</returns>
        internal async Task<bool> StartMonitoringAsync(int waitTimeoutMin = 0, int videoSegmentS = 10)
        {
            var success = false;
            var loopSuccess = false;
            var startDateTime = DateTime.Now;
            var tempVideoPath = Path.Combine(_tempDir, $"{_videoGuid}{VIDEO_EXTENSION}");

            while (!_globalCts.IsCancellationRequested)
            {
                // Получаем стартовый сегмент видео (определенной длины)
                try
                {
                    await RecordSegmentAsync(_rtspUrl, tempVideoPath, _globalCts.Token, TimeSpan.FromSeconds(videoSegmentS));
                    loopSuccess = true;
                    Debug.WriteLine($"{_camName}: StartMonitoringAsync");
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"{_camName}. Error: {ex.Message}");
                    await Task.Delay(TimeSpan.FromSeconds(_loopDelayS), _globalCts.Token); // Пауза при ошибке
                }

                // Проверяем, получен ли сегмент видео
                if (loopSuccess)
                {
                    loopSuccess = false;

                    // Если нет эталонного изображения
                    if (!_oldTempImgSet)
                    {
                        _oldTempImgSet = await SaveImageAsync(tempVideoPath, _tempDir, _etalonImgGuid);
                    }
                    else if (_oldTempImgSet && await SaveImageAsync(tempVideoPath, _tempDir, _newImgGuid)) // Если эталонное изображение существует
                    {
                        // Выбор метода сравнения
                        if (_motionDifMethod.HasFlag(MotionDifferenceMethod.OpenCV))
                        {
                            if (await HasMotionOpenCvAsync(_tempDir)) // Сравнение изображений с помощью OpenCV
                            {
                                success = true;
                                break;
                            }
                        }
                        else if (_motionDifMethod.HasFlag(MotionDifferenceMethod.Pixel))
                        {
                            if (await HasMotionPixelAsync(_tempDir)) // Сравнение изображений с помощью сравнения пикселей
                            {
                                success = true;
                                break;
                            }
                        }
                    }
                }

                // Проверка таймаута
                if (waitTimeoutMin > 0 && (DateTime.Now - startDateTime).TotalMinutes > waitTimeoutMin)
                {
                    break;
                }
            }

            return success;
        }

        /// <summary>
        /// Основной метод получения rtsp потока с записью видео в файлы и детектором движения
        /// </summary>
        /// <param name="nDelaySec">Колл-во секунд, по истечении которых проводится финальная проверка движения</param>
        /// <returns>Асинхронная задача</returns>
        internal async Task StartRecordingAsync(int nDelaySec = 15)
        {
            // Формируем путь к файлу
            var tempVideoPath = Path.Combine(_tempDir, $"{_videoGuid}{VIDEO_EXTENSION}");

            // Объявляем задачу мониторинга видеопотока
            var moutionTask = MotionMonitoringAsync(tempVideoPath, nDelaySec);

            // Объявляем задачу линейной записи видеопотока в файл
            var recordingTask = RecordingAsync(tempVideoPath);

            // Объявляем задачу мониторинга сети
            var networkTask = NetworkMonitoringAsync();

            // Запускаем и ожидаем завершение всех задач
            await Task.WhenAll([moutionTask, recordingTask, networkTask]);
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

            if (_tempCts != null && !_tempCts.IsCancellationRequested)
            {
                _tempCts.Cancel();
            }

            _globalCts?.Dispose();
            _tempCts?.Dispose();
        }

        /// <summary>
        /// Мониторинг движения
        /// </summary>
        /// <param name="tempVideoPath">Путь к временному файлу с видеозаписью</param>
        /// <param name="nDelaySec">Колл-во секунд, по истечении которых проводится финальная проверка движения</param>
        /// <returns>Асинхронная задача</returns>
        /// <exception cref="ArgumentException">Аргумент nDelaySec должен быть больше нуля</exception>
        private async Task MotionMonitoringAsync(string tempVideoPath, int nDelaySec = 15)
        {
            if (nDelaySec <= 0)
                throw new ArgumentException("The nDelaySec must be > 0");

            var startTimeStamp = DateTime.Now;                            // Получаем текущую дату и время
            var retryCount = 0;                                           // Счетчик  текущей итерации повтора
            var maxRetryCount = 3;                                        // Счетчик максимально возможных повторов
            var tempDelayS = NumRounding.Round(nDelaySec, maxRetryCount); // Получаем временный таймаут для каждой итерации цикла

            while (!_globalCts.IsCancellationRequested)
            {
                // Если нет эталонного изображения
                if (!_oldTempImgSet)
                {
                    _oldTempImgSet = await SaveImageAsync(tempVideoPath, _tempDir, _etalonImgGuid);
                }
                else if (_oldTempImgSet && await SaveImageAsync(tempVideoPath, _tempDir, _newImgGuid)) // Если эталонное изображение существует
                {
                    // Выбор метода сравнения
                    if (_motionDifMethod.HasFlag(MotionDifferenceMethod.OpenCV))
                    {
                        _hasMotion = await HasMotionOpenCvAsync(_tempDir); // Сравнение изображений с помощью OpenCV
                    }
                    else if (_motionDifMethod.HasFlag(MotionDifferenceMethod.Pixel))
                    {
                        _hasMotion = await HasMotionPixelAsync(_tempDir); // Сравнение изображений с помощью сравнения пикселей
                    }
                }

                // Сохраняем отрезок видео, если оно не сброшено и нет движения
                if (!_hasFlushedVideo && !_hasMotion)
                {
                    // Осуществляем проверку счетчика повторов проверки движения
                    if (retryCount < maxRetryCount)
                    {
                        retryCount++;
                    }
                    else // После N нулевых движений, сохраняем файл
                    {
                        _hasFlushedVideo = true;
                        _oldTempImgSet = false;

                        // Формируем путь для отрезка видео
                        var newVideoFilePath = _recordsDir
                            + $"[{startTimeStamp:dd.MM.yyyy HH-mm-ss}]"
                            + $"_[{DateTime.Now:dd.MM.yyyy HH-mm-ss}]"
                            + VIDEO_EXTENSION;

                        //////////////////////////////////////////////////////////////////////////////////
                        // Альтернативный способ получения временных меток,через FFProbe получаем
                        // продолжительность видео, и вычитаем ее из DateTime.Now
                        //////////////////////////////////////////////////////////////////////////////////
                        //var sTime = DateTime.Now - (await FFProbe.AnalyseAsync(tempVideoPath)).Duration;
                        //var eTime = DateTime.Now;
                        //
                        //newVideoFilePath = _recordsDir
                        //    + $"[{sTime:dd.MM.yyyy HH-mm-ss}]"
                        //    + $"_[{eTime:dd.MM.yyyy HH-mm-ss}]"
                        //    + VIDEO_EXTENSION;
                        //////////////////////////////////////////////////////////////////////////////////

                        // Копируем видео в каталог Records
                        File.Copy(tempVideoPath, newVideoFilePath, true);

                        Debug.WriteLine($"{_camName}: part video saved, path: {newVideoFilePath}");

                        // Отправляем запрос на отмену
                        _tempCts.Cancel();
                    }
                }
                else if (_hasFlushedVideo && _hasMotion) // Если видео сброшено и появилось движение
                {
                    retryCount = 0;
                    _hasFlushedVideo = false;
                    startTimeStamp = DateTime.Now;
                }
                else // В остальных случаях
                {
                    retryCount = 0;
                }

                // Задержка, перед следующей итерацией цикла
                await Task.Delay(TimeSpan.FromSeconds(tempDelayS), _globalCts.Token);
            }

            Debug.WriteLine($"{_camName}: MotionMonitoringAsync exited from while!");
        }

        /// <summary>
        /// Запись видео в файл с поддержкой временного CTS (для остановки и запуска записи)
        /// </summary>
        /// <param name="tempVideoPath">Путь к временному файлу с видеозаписью</param>
        /// <returns>Асинхронная задача</returns>
        private async Task RecordingAsync(string tempVideoPath)
        {
            while (!_globalCts.IsCancellationRequested)
            {
                // Инициализируем временный CTS
                _tempCts = new CancellationTokenSource();

                try
                {
                    // Создаем LTS, для передачи нескольких CTS в метод
                    var linkedCTS = CancellationTokenSource.CreateLinkedTokenSource(_globalCts.Token, _tempCts.Token);

                    // Запускаем запись видео в файл
                    await RecordSegmentAsync(_rtspUrl, tempVideoPath, linkedCTS.Token);
                }
                catch (OperationCanceledException)
                {
                    if (_globalCts.IsCancellationRequested)
                        break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"{_camName}. Error: {ex.Message}");
                }

                await Task.Delay(TimeSpan.FromSeconds(_loopDelayS), _globalCts.Token); // Пауза
            }

            Debug.WriteLine($"{_camName}: RecordingAsync exited from while!");
        }

        /// <summary>
        /// Мониторинг сети на наличие ошибок
        /// </summary>
        /// <param name="waitMaxS">Колл-во секунд, по истечении которых вызывается омена tempCTS</param>
        /// <returns>Асинхронная задача</returns>
        private async Task NetworkMonitoringAsync(int waitMaxS = 60)
        {
            var retryCount = 0;                                          // Счетчик  текущей итерации повтора
            var maxRetryCount = 3;                                       // Счетчик максимально возможных повторов
            var tempDelayS = NumRounding.Round(waitMaxS, maxRetryCount); // Получаем временный таймаут для каждой итерации цикла

            while (!_globalCts.IsCancellationRequested)
            {
                if (_etalonFrameTime == _lastFrameTime)
                {
                    // Осуществляем проверку счетчика повторов
                    if (retryCount < maxRetryCount)
                    {
                        retryCount++;
                    }
                    else
                    {
                        Debug.WriteLine($"{_camName}: network faluring detected. Cancel video recording!");

                        retryCount = 0;
                        _tempCts.Cancel(); // Вызываем отмену задачи захвата видеопотока
                    }
                }
                else
                {
                    retryCount = 0;
                    _etalonFrameTime = _lastFrameTime;
                }

                await Task.Delay(TimeSpan.FromSeconds(tempDelayS), _globalCts.Token); // Пауза
            }

            Debug.WriteLine($"{_camName}: NetworkMonitoringAsync exited from while!");
        }

        /// <summary>
        /// Получает информацию о видеопотоке
        /// </summary>
        /// <param name="operationTimeoutS">Таймаут асинхронной операции</param>
        /// <returns>Описание параметров видеопотока</returns>
        /// <exception cref="ArgumentException">Аргумент operationTimeoutS должен быть больше нуля</exception>
        internal async Task<string> GetVideoInfoAsync(int operationTimeoutS = 10)
        {
            if (operationTimeoutS <= 0)
                throw new ArgumentException("The operationTimeoutS must be > 0");

            var result = string.Empty;

            try
            {
                var videoInfo = await AsyncExt.TimeoutAsync(FFProbe.AnalyseAsync(new Uri(_rtspUrl)), TimeSpan.FromSeconds(operationTimeoutS));

                if (videoInfo == null || videoInfo.VideoStreams == null || videoInfo.VideoStreams.Count <= 0)
                    return string.Empty;

                foreach (var stream in videoInfo.VideoStreams)
                {
                    if (stream != null && stream.Profile == "Main")
                    {
                        var pixelFormatInfo = stream.GetPixelFormatInfo();
                        result += $"Resolution: {stream.Width}x{stream.Height}" + Environment.NewLine;
                        result += $"FPS: {stream.FrameRate}" + Environment.NewLine;
                        result += $"Format: {stream.PixelFormat}" + Environment.NewLine;
                        result += $"Bits per pixel: {pixelFormatInfo.BitsPerPixel}" + Environment.NewLine;
                        result += $"Codec name: {stream.CodecName}" + Environment.NewLine + Environment.NewLine;
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{_camName}. Error while analyzing the stream: {ex.Message}");
                return result;
            }
        }

        /// <summary>
        /// Записывает видео в файл с поддержкой сегментации
        /// </summary>
        /// <param name="url">Строковой Uri вида: rtsp://login:password@ip:port/</param>
        /// <param name="filePath">Путь для сохранения файла</param>
        /// <param name="token">Токен отмены асинхронной задачи</param>
        /// <param name="segmentTimePeriod">Продолжительность видеозаписи + таймаут асинхронной операции</param>
        /// <returns>Асинхронная задача</returns>
        private async Task RecordSegmentAsync(string uri, string filePath, CancellationToken token, TimeSpan? segmentTimePeriod = null)
        {
            var workTask = FFMpegArguments
                .FromUrlInput(new Uri(uri))
                .OutputToFile(filePath, overwrite: true, options =>
                {
                    // Отключаем многопоточную обработку
                    options.UsingMultithreading(false);

                    //options.WithHardwareAcceleration(HardwareAccelerationDevice.CUDA); // Отключено, т.к. неизвестно, используется ли GPU ускорение

                    // Этот параметр заставляет использовать системные временные метки вместо тех, что могут быть в самом потоке данных
                    options.WithCustomArgument("-use_wallclock_as_timestamps 1");

                    // Этот параметр заставляет FFmpeg сбросить и начать заново временные метки (PTS/DTS) внутри обработанных сегментов или
                    // выходных файлов, чтобы они начинались с нуля
                    options.WithCustomArgument("-reset_timestamps 1");

                    // 1. -movflags: параметр, управляющий специальными настройками для мультиплексора (muxer)

                    // 2. empty_moov: создает исходный MP4 файл без moov atom

                    // 3. frag_keyframe: заставляет FFmpeg фрагментировать MP4 файл. Вместо одного непрерывного блока данных,
                    // видео разбивается на независимые фрагменты(части), каждый из которых начинается с ключевого кадра (I - frame)

                    // 4. faststart: по умолчанию MP4 файл хранит критически важную мета-информацию (индекс всех фрагментов видео и аудио,
                    // называемый moov atom) в конце файла. Это значит, что плеер (например браузер) должен скачать весь файл целиком,
                    // чтобы начать воспроизведение. Флаг faststart перемещает этот moov atom в начало файла
                    options.WithCustomArgument("-movflags frag_keyframe+empty_moov+faststart");

                    // Указание FFmpeg не перекодировать видео, а просто скопировать видеопоток из исходного файла в выходной "as is"
                    options.WithVideoCodec("copy");

                    // Этот параметр выставляет аудио-кодек AAC, согласно спецификации MPEG-4 
                    options.WithAudioCodec(AudioCodec.Aac);

                    // Устанавливаем продолжительность выходного файла
                    if (segmentTimePeriod != null && segmentTimePeriod?.TotalMilliseconds > 0)
                        options.WithDuration(segmentTimePeriod);
                })
                .NotifyOnProgress((progress) => _lastFrameTime = (ulong)progress.TotalSeconds) // Отслеживаем процесс выполнения
                .CancellableThrough(token) // Добавляем CT в сессию
                .ProcessAsynchronously(true);

            if (segmentTimePeriod != null && segmentTimePeriod?.TotalMilliseconds > 0)
            {
                await AsyncExt.TimeoutAsync(
                    workTask,
                    TimeSpan.FromMilliseconds((int)segmentTimePeriod?.TotalMilliseconds! + 5000)); // Добавляем +5 секунд к таймауту асинхронной операции
            }
            else await workTask;
        }

        /// <summary>
        /// Сохраняет изображение во временную директорию
        /// </summary>
        /// <param name="tempVideoPath">Путь к временному файлу с видеозаписью</param>
        /// <param name="tempDir">Временная директория</param>
        /// <param name="fileNameWithoutExt">Имя файла без расширения</param>
        /// <param name="operationTimeoutS">Таймаут асинхронной операции</param>
        /// <returns>True - файл успешно сохранен, False - не удалось сохранить файл</returns>
        /// <exception cref="ArgumentException">Аргумент operationTimeoutS должен быть больше нуля</exception>
        private async Task<bool> SaveImageAsync(string tempVideoPath, string tempDir, string fileNameWithoutExt,
            int operationTimeoutS = 7)
        {
            if (operationTimeoutS <= 0)
                throw new ArgumentException("The operationTimeoutS must be > 0");

            try
            {
                // Формируем путь к снапшоту
                var snapshotPath = tempDir + fileNameWithoutExt + IMG_EXTENSION;

                // Получаем и сохраняем изображение из памяти
                return await AsyncExt.TimeoutAsync(
                    FFMpeg.SnapshotAsync(tempVideoPath, snapshotPath, new Size(1920, 1080)),
                    TimeSpan.FromSeconds(operationTimeoutS));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{_camName}. Error saving image: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Детектор движения, сравнивает два изображения через OpenCV
        /// </summary>
        /// <param name="tempDir">Временная директория</param>
        /// <param name="operationTimeoutS">Таймаут асинхронной операции</param>
        /// <returns>True - есть движение, False - нет движения</returns>
        /// <exception cref="ArgumentException">Аргумент operationTimeoutS должен быть больше нуля</exception>
        private async Task<bool> HasMotionOpenCvAsync(string tempDir, int operationTimeoutS = 30)
        {
            if (operationTimeoutS <= 0)
                throw new ArgumentException("The operationTimeoutS must be > 0");

            var hasMotion = false;

            // Формируем путь к снапшотам
            var etalonImgPath = tempDir + _etalonImgGuid + IMG_EXTENSION;
            var newImgPath = tempDir + _newImgGuid + IMG_EXTENSION;

            // Запускаем в потоке из пула (для предотвращения блокировки UI на период чтения и синхронного сравнения)
            var workTask = Task.Run(() =>
            {
                // Читаем изображения
                using (var bmp1 = new Bitmap(etalonImgPath))
                using (var bmp2 = new Bitmap(newImgPath))
                {
                    // Настройка детектора
                    using var motionDetector = new MotionDetector();
                    motionDetector.MinContourArea = 1000;    // Большая площадь - меньше ложных срабатываний
                    motionDetector.ThresholdValue = 40;      // Более высокий порог - меньше чувствительность
                    motionDetector.BlurSize = 15;            // Меньшее размытие - более точное детектирование

                    // Простое детектирование (обнаружено движение или нет)
                    hasMotion = motionDetector.DetectMotion(bmp1, bmp2);
                }

                Debug.WriteLine($@"{_camName}. OpenCV motion detector: " + (hasMotion ? "motion" : "no motion"));

                // Заменяем эталонное изображение на новое
                if (hasMotion)
                {
                    File.Move(newImgPath, etalonImgPath, true);
                }
            });

            await AsyncExt.TimeoutAsync(workTask, TimeSpan.FromSeconds(operationTimeoutS));

            return hasMotion;
        }

        /// <summary>
        /// Детектор движения, сравнивает два изображения по пикселям
        /// </summary>
        /// <param name="tempDir">Временная директория</param>
        /// <param name="operationTimeoutS">Таймаут асинхронной операции</param>
        /// <returns>True - есть движение, False - нет движения</returns>
        /// <exception cref="ArgumentException">Аргумент operationTimeoutS должен быть больше нуля</exception>
        private async Task<bool> HasMotionPixelAsync(string tempDir, int operationTimeoutS = 30)
        {
            if (operationTimeoutS <= 0)
                throw new ArgumentException("The operationTimeoutS must be > 0");

            var hasMotion = false;

            // Порог чувствительности движения
            const double pixel_motion_threshold = 0.01; // Пример: 0.01 - небольшие объекты, 0.025 - крупные объекты

            // Формируем путь к снапшотам
            var etalonImgPath = tempDir + _etalonImgGuid + IMG_EXTENSION;
            var newImgPath = tempDir + _newImgGuid + IMG_EXTENSION;

            // Запускаем в потоке из пула (для предотвращения блокировки UI на период чтения и синхронного сравнения)
            var workTask = Task.Run(() =>
            {
                // Читаем изображения
                using (var bmp1 = new Bitmap(etalonImgPath))
                using (var bmp2 = new Bitmap(newImgPath))
                {
                    // Получаем разность изображений (проверяем каждый второй пиксель)
                    var threshold = ImageDifference.CalculateN(bmp1, bmp2, 2);

                    // Округляем значение до ближайшего целого, ограничив тремя знаками после запятой
                    threshold = Math.Round(threshold, 3);

                    // Сравниваем полученную разность с порогом чувствительности
                    if (threshold >= pixel_motion_threshold)
                    {
                        hasMotion = true; // Обнаружено движение
                    }

                    Debug.WriteLine($@"{_camName}. Pixel motion detector: "
                        + (hasMotion ? "motion" : "no motion") + $" - {threshold} | {pixel_motion_threshold}");
                }

                // Заменяем эталонное изображение на новое
                if (hasMotion)
                {
                    File.Move(newImgPath, etalonImgPath, true);
                }
            });

            await AsyncExt.TimeoutAsync(workTask, TimeSpan.FromSeconds(operationTimeoutS));

            return hasMotion;
        }
    }
}