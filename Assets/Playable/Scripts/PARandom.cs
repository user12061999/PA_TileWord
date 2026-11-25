namespace PA.TileWord
{
    /// <summary>
    /// Bộ sinh số ngẫu nhiên có seed (Park–Miller) để board luôn giống nhau giữa các lần chạy, không phụ thuộc
    /// UnityEngine.Random. Tính bằng double (chính xác tới 2^53) để tránh long, vốn bị giả lập chậm trong JS của Luna.
    /// </summary>
    public class PARandom
    {
        private const double Modulus = 2147483647.0;
        private const double Multiplier = 16807.0;
        private double _state;

        public PARandom(int seed)
        {
            int s = seed % 2147483646;
            if (s <= 0) s += 2147483646;
            _state = s;
        }

        /// <summary>Số thực trong [0, 1).</summary>
        public double Next01()
        {
            double product = _state * Multiplier;
            _state = product - System.Math.Floor(product / Modulus) * Modulus;
            return (_state - 1.0) / (Modulus - 1.0);
        }

        /// <summary>Số nguyên trong [min, max).</summary>
        public int Range(int min, int max)
        {
            if (max <= min) return min;
            int value = min + (int)(Next01() * (max - min));
            return value >= max ? max - 1 : value;
        }
    }
}
