namespace RtspVideoCapturing.Recorder.Helper
{
    /// <summary>
    /// Класс для округления чисел
    /// </summary>
    internal class NumRounding
    {
        /// <summary>
        /// Округление в большую сторону при делении двух целых чисел
        /// </summary>
        /// <param name="num1">Первое число</param>
        /// <param name="num2">Второе число</param>
        /// <returns></returns>
        internal static int Round(int num1, int num2)
        {
            if (num1 % num2 == 0)
            {
                return num1 / num2; // Делится без остатка
            }
            else
            {
                return (int)Math.Ceiling((double)num1 / num2); // Округляем в большую сторону
            }
        }
    }
}