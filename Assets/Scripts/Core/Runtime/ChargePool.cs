namespace Tavla.Core.Runtime
{
    /// <summary>
    /// Usage charge counter. The shared bookkeeping of single-use items is written here, in ONE place.
    ///
    /// Why a separate class? So that single-use items are not forced to derive from the same base class.
    /// A relic may derive from ConsumableRelic, while a future "trap card" may live in a completely
    /// different hierarchy — both hold this pool as a field (composition over inheritance).
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

        /// <summary>Spends one charge. Returns false and does NOT change the counter if none is left.</summary>
        public bool TryConsume()
        {
            if (IsEmpty)
                return false;

            Remaining--;
            return true;
        }

        /// <summary>Recharge in the shop.</summary>
        public void Refill()
        {
            Remaining = Max;
        }

        /// <summary>Adds charges without exceeding the maximum.</summary>
        public void Grant(int amount)
        {
            Remaining += amount;
            if (Remaining > Max)
                Remaining = Max;
        }
    }
}
