using System.Drawing.Imaging;
using System.Runtime.Versioning;

namespace RtspVideoCapturing.Recorder.Image
{
    /// <summary>
    /// Детектор движения, сравнивает два изображения по пикселям
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal static class ImageDifference
    {
        /// <summary>
        /// Проводит сравнение каждого N пикселя между двумя изображениями
        /// </summary>
        /// <param name="bmp1">Первое изображение</param>
        /// <param name="bmp2">Второе изображение</param>
        /// <param name="step">Шаг для пропуска пикселей</param>
        /// <returns>Порог совпадения между изображениями (пример: 0 - изображения идентичны, 0.01 - изображения не совпадают)</returns>
        /// <exception cref="ArgumentException">Возникает в случае отличия размера между изображениями</exception>
        /// <exception cref="ArgumentException">Возникает, когда step <= 0, аргемент step должен быть положительным</exception>
        internal unsafe static double CalculateN(Bitmap bmp1, Bitmap bmp2, int step = 2)
        {
            // Размер изображений должен быть одинаковым
            if (bmp1.Size != bmp2.Size)
                throw new ArgumentException("The images must be the same size");

            // Проверяем, чтобы шаг был положительным
            if (step <= 0)
                throw new ArgumentException("The step must be greater than zero");

            var totalDifference = 0D;
            const int bytesPerPixel = 3;    // Для Format24bppRgb
            var totalPixels = 0;            // Счетчик обработанных пикселей

            // Блокируем битмапы в памяти для быстрого доступа к пикселям
            var bd1 = bmp1.LockBits(new Rectangle(0, 0, bmp1.Width, bmp1.Height),
                                             ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            var bd2 = bmp2.LockBits(new Rectangle(0, 0, bmp2.Width, bmp2.Height),
                                             ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

            var ptr1 = (byte*)bd1.Scan0;
            var ptr2 = (byte*)bd2.Scan0;

            // Проходим только по каждому третьему пикселю по X и Y с проверкой границ
            for (var y = 0; y < bmp1.Height; y += step)
            {
                // Проверяем, не вышли ли за границы по Y
                if (y >= bmp1.Height)
                    break;

                for (var x = 0; x < bmp1.Width; x += step)
                {
                    // Проверяем, не вышли ли за границы по X
                    if (x >= bmp1.Width)
                        break;

                    var index = y * bd1.Stride + x * bytesPerPixel;

                    // Дополнительная проверка выхода за границы буфера
                    if (index + bytesPerPixel > bd1.Stride * bmp1.Height 
                        || index + bytesPerPixel > bd2.Stride * bmp2.Height)
                    {
                        continue; // Пропускаем пиксели у границы изображения
                    }

                    var r1 = ptr1[index + 2];
                    var g1 = ptr1[index + 1];
                    var b1 = ptr1[index];
                    var r2 = ptr2[index + 2];
                    var g2 = ptr2[index + 1];
                    var b2 = ptr2[index];

                    totalDifference += Math.Abs(r1 - r2) / 255.0;
                    totalDifference += Math.Abs(g1 - g2) / 255.0;
                    totalDifference += Math.Abs(b1 - b2) / 255.0;

                    totalPixels++; // Увеличиваем счетчик обработанных пикселей
                }
            }

            bmp1.UnlockBits(bd1);
            bmp2.UnlockBits(bd2);

            // Защита от деления на ноль
            if (totalPixels == 0)
                return 0;

            // Нормализуем разницу на количество обработанных пикселей и каналов
            return totalDifference / (totalPixels * bytesPerPixel);
        }

        /// <summary>
        /// Проводит сравнение каждого пикселя между двумя изображениями
        /// </summary>
        /// <param name="bmp1">Первое изображение</param>
        /// <param name="bmp2">Второе изображение</param>
        /// <returns>Порог совпадения между изображениями (пример: 0 - изображения идентичны, 0.01 - изображения не совпадают)</returns>
        /// <exception cref="ArgumentException">Возникает в случае отличия размера между изображениями</exception>
        internal unsafe static double Calculate(Bitmap bmp1, Bitmap bmp2)
        {
            // Размер изображений должен быть одинаковым
            if (bmp1.Size != bmp2.Size)
                throw new ArgumentException("The images must be the same size");

            var totalDifference = 0D;
            const int bytesPerPixel = 3; // Для Format24bppRgb
            var totalPixels = bmp1.Width * bmp1.Height;

            // Блокируем битмапы в памяти для быстрого доступа к пикселям
            var bd1 = bmp1.LockBits(new Rectangle(0, 0, bmp1.Width, bmp1.Height),
                                             ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            var bd2 = bmp2.LockBits(new Rectangle(0, 0, bmp2.Width, bmp2.Height),
                                             ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

            var ptr1 = (byte*)bd1.Scan0;
            var ptr2 = (byte*)bd2.Scan0;

            for (var y = 0; y < bmp1.Height; y++)
            {
                for (var x = 0; x < bmp1.Width; x++)
                {
                    var index = y * bd1.Stride + x * bytesPerPixel;
                    var r1 = ptr1[index + 2];
                    var g1 = ptr1[index + 1];
                    var b1 = ptr1[index];
                    var r2 = ptr2[index + 2];
                    var g2 = ptr2[index + 1];
                    var b2 = ptr2[index];

                    totalDifference += Math.Abs(r1 - r2) / 255.0;
                    totalDifference += Math.Abs(g1 - g2) / 255.0;
                    totalDifference += Math.Abs(b1 - b2) / 255.0;
                }
            }

            bmp1.UnlockBits(bd1);
            bmp2.UnlockBits(bd2);

            return totalDifference / (totalPixels * bytesPerPixel); // Нормализуем разницу
        }
    }
}