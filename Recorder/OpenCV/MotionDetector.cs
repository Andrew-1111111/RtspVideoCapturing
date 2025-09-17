using OpenCvSharp;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using Size = OpenCvSharp.Size;

namespace RtspVideoCapturing.Recorder.OpenCV
{
    /// <summary>
    /// Детектор движения, сравнивает два изображения через OpenCV
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal class MotionDetector : IDisposable
    {
        private const string IMG_EXTENSION = ".png";

        private readonly Mat _previousFrame = null!;
        private readonly Mat _currentFrame = null!;
        private readonly Mat _grayPrevious = null!;
        private readonly Mat _grayCurrent = null!;
        private readonly Mat _frameDelta = null!;
        private readonly Mat _thresh = null!;
        private readonly Mat _kernel = null!;

        // Настройки детектирования
        internal double MinContourArea { get; set; } = 1000;    // Минимальная площадь контура (большая площадь - меньше ложных срабатываний)
        internal int ThresholdValue { get; set; } = 30;         // Порог для бинаризации (более высокий порог - меньше чувствительность)
        internal double BlurSize { get; set; } = 15;            // Размер размытия (меньшее размытие - более точное детектирование)

        internal MotionDetector()
        {
            _kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(5, 5));
        }

        internal bool DetectMotion(Bitmap bmp1, Bitmap bmp2)
        {
            try
            {
                // Конвертируем Bitmap в Mat
                using var mat1 = BitmapToMat(bmp1);
                using var mat2 = BitmapToMat(bmp2);

                // Преобразуем в grayscale
                using var gray1 = new Mat();
                using var gray2 = new Mat();
                Cv2.CvtColor(mat1, gray1, ColorConversionCodes.BGR2GRAY);
                Cv2.CvtColor(mat2, gray2, ColorConversionCodes.BGR2GRAY);

                // Размываем для уменьшения шума
                Cv2.GaussianBlur(gray1, gray1, new Size(BlurSize, BlurSize), 0);
                Cv2.GaussianBlur(gray2, gray2, new Size(BlurSize, BlurSize), 0);

                // Вычисляем разницу между кадрами
                using var frameDelta = new Mat();
                Cv2.Absdiff(gray1, gray2, frameDelta);

                // Применяем пороговую обработку
                using var thresh = new Mat();
                Cv2.Threshold(frameDelta, thresh, ThresholdValue, 255, ThresholdTypes.Binary);

                // Морфологические операции для заполнения дыр
                Cv2.Dilate(thresh, thresh, _kernel, iterations: 2);
                Cv2.Erode(thresh, thresh, _kernel, iterations: 1);

                // Получаем контуры
                var contours = thresh.FindContoursAsArray(
                    RetrievalModes.External,
                    ContourApproximationModes.ApproxSimple);

                // Ищем контуры с достаточной площадью
                foreach (var contour in contours)
                {
                    var area = Cv2.ContourArea(contour);
                    if (area > MinContourArea)
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Method: DetectMotion. Error in motion detection: {ex.Message}");
            }

            return false;
        }

        internal bool DetectMotion(Bitmap bitmap1, Bitmap bitmap2, out Rectangle motionRegion)
        {
            motionRegion = Rectangle.Empty;

            try
            {
                // Конвертируем Bitmap в Mat
                using var mat1 = BitmapToMat(bitmap1);
                using var mat2 = BitmapToMat(bitmap2);

                // Преобразуем в grayscale
                using var gray1 = new Mat();
                using var gray2 = new Mat();
                Cv2.CvtColor(mat1, gray1, ColorConversionCodes.BGR2GRAY);
                Cv2.CvtColor(mat2, gray2, ColorConversionCodes.BGR2GRAY);

                // Размываем для уменьшения шума
                Cv2.GaussianBlur(gray1, gray1, new Size(BlurSize, BlurSize), 0);
                Cv2.GaussianBlur(gray2, gray2, new Size(BlurSize, BlurSize), 0);

                // Вычисляем разницу между кадрами
                using var frameDelta = new Mat();
                Cv2.Absdiff(gray1, gray2, frameDelta);

                // Применяем пороговую обработку
                using var thresh = new Mat();
                Cv2.Threshold(frameDelta, thresh, ThresholdValue, 255, ThresholdTypes.Binary);

                // Морфологические операции для заполнения дыр
                Cv2.Dilate(thresh, thresh, _kernel, iterations: 2);
                Cv2.Erode(thresh, thresh, _kernel, iterations: 1);

                // Находим контуры
                var contours = thresh.FindContoursAsArray(
                    RetrievalModes.External,
                    ContourApproximationModes.ApproxSimple);

                // Ищем контуры с достаточной площадью
                foreach (var contour in contours)
                {
                    var area = Cv2.ContourArea(contour);
                    if (area > MinContourArea)
                    {
                        var boundingRect = Cv2.BoundingRect(contour);
                        motionRegion = new Rectangle(
                            boundingRect.X,
                            boundingRect.Y,
                            boundingRect.Width,
                            boundingRect.Height);

                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Method: DetectMotion. Error in motion detection: {ex.Message}");
            }

            return false;
        }

        internal bool DetectMotionWithDebug(Bitmap bitmap1, Bitmap bitmap2, out Bitmap debugImage)
        {
            var motionDetected = false;
            debugImage = null!;

            try
            {
                using var mat1 = BitmapToMat(bitmap1);
                using var mat2 = BitmapToMat(bitmap2);
                using var resultMat = mat2.Clone();

                // Преобразуем в grayscale
                using var gray1 = new Mat();
                using var gray2 = new Mat();
                Cv2.CvtColor(mat1, gray1, ColorConversionCodes.BGR2GRAY);
                Cv2.CvtColor(mat2, gray2, ColorConversionCodes.BGR2GRAY);

                // Размываем для уменьшения шума
                Cv2.GaussianBlur(gray1, gray1, new Size(BlurSize, BlurSize), 0);
                Cv2.GaussianBlur(gray2, gray2, new Size(BlurSize, BlurSize), 0);

                // Вычисляем разницу между кадрами
                using var frameDelta = new Mat();
                Cv2.Absdiff(gray1, gray2, frameDelta);

                // Применяем пороговую обработку
                using var thresh = new Mat();
                Cv2.Threshold(frameDelta, thresh, ThresholdValue, 255, ThresholdTypes.Binary);

                // Морфологические операции для заполнения дыр
                Cv2.Dilate(thresh, thresh, _kernel, iterations: 2);
                Cv2.Erode(thresh, thresh, _kernel, iterations: 1);

                // Получаем контуры
                var contours = thresh.FindContoursAsArray(
                    RetrievalModes.External,
                    ContourApproximationModes.ApproxSimple);

                // Ищем контуры с достаточной площадью
                foreach (var contour in contours)
                {
                    var area = Cv2.ContourArea(contour);
                    if (area > MinContourArea)
                    {
                        var boundingRect = Cv2.BoundingRect(contour);
                        Cv2.Rectangle(resultMat, boundingRect, new Scalar(0, 255, 0), 2);
                        motionDetected = true;
                    }
                }

                if (motionDetected)
                {
                    var bmpPart = MatToBitmap(resultMat);
                    if (bmpPart != null)
                    {
                        debugImage = bmpPart;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Method: DetectMotionWithDebug. Error: {ex.Message}");
            }

            return motionDetected;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Mat BitmapToMat(Bitmap bitmap)
        {
            // Конвертируем в Format24bppRgb (если нужно)
            if (bitmap.PixelFormat != PixelFormat.Format24bppRgb)
            {
                var converted = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format24bppRgb);
                using var g = Graphics.FromImage(converted);
                g.DrawImage(bitmap, 0, 0, bitmap.Width, bitmap.Height);
                return BitmapToMat(converted);
            }

            var bitmapData = bitmap.LockBits(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.ReadOnly,
                PixelFormat.Format24bppRgb);

            try
            {
                // Используем Mat.FromPixelData (конструктор new Mat() - depercated)
                return Mat.FromPixelData(
                    bitmapData.Height,
                    bitmapData.Width,
                    MatType.CV_8UC3,
                    bitmapData.Scan0,
                    bitmapData.Stride);
            }
            finally
            {
                bitmap.UnlockBits(bitmapData);
            }
        }

        private static Bitmap? MatToBitmap(Mat mat)
        {
            try
            {
                var ms = new MemoryStream();

                // Конвертируем Mat в массив байт
                var imageBytes = mat.ToBytes(IMG_EXTENSION);

                // Создаем Bitmap из массива байт
                ms.Write(imageBytes, 0, imageBytes.Length);
                ms.Position = 0;

                return new Bitmap(ms);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Method: MatToBitmap. Error converting Mat to Bitmap: {ex.Message}");
                return null;
            }
        }

        public void Dispose()
        {
            _previousFrame?.Dispose();
            _currentFrame?.Dispose();
            _grayPrevious?.Dispose();
            _grayCurrent?.Dispose();
            _frameDelta?.Dispose();
            _thresh?.Dispose();
            _kernel?.Dispose();
        }
    }
}