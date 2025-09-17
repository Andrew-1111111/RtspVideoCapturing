using RtspVideoCapturing.Recorder;
using RtspVideoCapturing.Recorder.Helper;

namespace RtspVideoCapturing
{
    public partial class Main : Form
    {
        private static VideoRecorder _vRecorder = null!;
        private static AdvancedVideoRecorder _advRecorder = null!;

        public Main()
        {
            InitializeComponent();

        }

        private async void Main_Shown(object sender, EventArgs e)
        {
            // Убиваем FFMpeg
            await Task.Run(ProcessKiller.Kill_FFMpeg);
            await Task.Delay(100);
        }

        private async void Main_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Убиваем FFMpeg
            await Task.Run(ProcessKiller.Kill_FFMpeg);
            await Task.Delay(100);

            // Завершает этот процесс и возвращает код выхода операционной системе
            Environment.Exit(0);
        }

        private async void Button_RunSingleCam_Click(object sender, EventArgs e)
        {
            Button_RunSingleCam.Enabled = false;

            // Устанавливаем базовую директорию
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var recordsDir = baseDir + @"Records\";
            var tempDir = baseDir + @"Temp\";

            // Проверяем директорию для видеозаписей
            if (!Directory.Exists(recordsDir))
            {
                Directory.CreateDirectory(recordsDir);
            }

            // Проверяем и очищаем временную директорию
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
                Directory.CreateDirectory(tempDir);
            }
            else Directory.CreateDirectory(tempDir);

            // Инициализируем CTS
            using var globalCTS = new CancellationTokenSource();

            // Инициализируем и заполняем параметры класса-рекордера
            _vRecorder = new VideoRecorder(
                "Cam1",
#error нужно установить корректный rtsp адрес в формате: rtsp://login:password@ip:port/ или rtsp://ip:port/
                "rtsp://____________________________",
                tempDir,
                recordsDir,
                VideoRecorder.MotionDifferenceMethod.OpenCV, // OpenCV режим детектора движения
                globalCTS);

            try
            {
                // Анализируем информацию о видеопотоке
                Label_VideoFormat.Text = await _vRecorder.GetVideoInfoAsync();

                // Запускаем первичный метод получения rtsp потока с детектором движения
                if (await _vRecorder.StartMonitoringAsync())
                {
                    // Запускаем основной метод получения rtsp потока с записью видео в файлы и детектором движения
                    await _vRecorder.StartRecordingAsync();
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

            Label_VideoFormat.Text = "Stopped";

            Button_RunSingleCam.Enabled = true;
        }

        private async void Button_RunAllCams_Click(object sender, EventArgs e)
        {
            Button_RunAllCams.Enabled = false;

            // Устанавливаем базовую директорию
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var recordsDir = baseDir + @"Records\";
            var tempDir = baseDir + @"Temp\";

            // Проверяем директорию для видеозаписей
            if (!Directory.Exists(recordsDir))
            {
                Directory.CreateDirectory(recordsDir);
            }

            // Проверяем и очищаем временную директорию
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
                Directory.CreateDirectory(tempDir);
            }
            else Directory.CreateDirectory(tempDir);

            // Инициализируем CTS
            using var globalCTS = new CancellationTokenSource();

            // Инициализируем список с камерами
            var cams = new List<(string, string)>()
            {
#error нужно установить корректные rtsp адреса в формате: rtsp://login:password@ip:port/ или rtsp://ip:port/
                ("Cam1", "rtsp://_________________________________"),
                ("Cam2", "rtsp://_________________________________")
            };

            // Инициализируем и заполняем параметры класса-рекордера
            _advRecorder = new AdvancedVideoRecorder(
                cams,
                tempDir,
                recordsDir,
                globalCTS);

            try
            {
                // Выводим информацию, что запускаем задачи
                Label_VideoFormat.Text = cams[0].Item1 + ": runned" + Environment.NewLine;
                Label_VideoFormat.Text += cams[1].Item1 + ": runned" + Environment.NewLine;
                Label_VideoFormat.Text += cams[2].Item1 + ": runned" + Environment.NewLine;
                Label_VideoFormat.Text += cams[3].Item1 + ": runned" + Environment.NewLine;

                // Запуск захвата видеопотока с сохранением в файлы (+ детектор движения) для нескольких камер
                await _advRecorder.RunRecordsAsync();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

            Label_VideoFormat.Text = "Stopped";

            Button_RunAllCams.Enabled = true;
        }

        private async void Button_Cancel_Click(object sender, EventArgs e)
        {
            Button_Cancel.Enabled = false;
            Button_RunSingleCam.Enabled = true;

            if (!Button_RunSingleCam.Enabled)
            {
                Button_RunSingleCam.Enabled = true;
                _vRecorder.StopRecording(); // Останавливаем единственное подключение
            }

            if (!Button_RunAllCams.Enabled)
            {
                Button_RunAllCams.Enabled = true;
                _advRecorder.StopRecording(); // Останавливаем множество подключений
            }

            // Убиваем FFMpeg
            await Task.Run(ProcessKiller.Kill_FFMpeg);
            await Task.Delay(100);

            Button_Cancel.Enabled = true;
        }
    }
}