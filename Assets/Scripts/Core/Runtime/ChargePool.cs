namespace Tavla.Core.Runtime
{
    /// <summary>
    /// Kullanim hakki sayaci. Tek kullanimlik ogelerin ortak defter tutma isi burada,
    /// TEK yerde yazili.
    ///
    /// Neden ayri bir sinif? Cunku her tek kullanimlik oge ayni ata siniftan turemek
    /// zorunda kalmasin. Bir tilsim ConsumableRelic'ten tureyebilir ama ileride
    /// eklenecek bir "tuzak karti" bambaska bir agacta olabilir — ikisi de bu havuzu
    /// alan olarak tutar (kalitim degil kompozisyon).
    /// </summary>
    public class ChargePool
    {
        public int Max { get; private set; }
        public int Remaining { get; private set; }

        public bool IsEmpty => Remaining <= 0;

        public ChargePool(int max = 1)
        {
            Max = max < 1 ? 1 : max;
            Remaining = Max;
        }

        /// <summary>Bir hak harcar. Hak yoksa false doner ve sayac DEGISMEZ.</summary>
        public bool TryConsume()
        {
            if (IsEmpty)
                return false;

            Remaining--;
            return true;
        }

        /// <summary>Dukkanda yeniden dolum.</summary>
        public void Refill()
        {
            Remaining = Max;
        }

        /// <summary>Tavani asmadan hak ekler.</summary>
        public void Grant(int amount)
        {
            Remaining += amount;
            if (Remaining > Max)
                Remaining = Max;
        }
    }
}
