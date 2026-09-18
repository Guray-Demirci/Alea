using System.Collections.Generic;

namespace Tavla.Core.Runtime
{
    /// <summary>
    /// Bir eldeki zar durumu. Iki farkli tuketim modu vardir:
    ///   Normal mod  : Atilan degerler tek tek harcanir (klasik tavla).
    ///   FreeSplit   : "Kartografin Hilesi" aktifse zarlarin TOPLAMI havuz olur ve
    ///                 istenen buyuklukte parcalara bolunerek istenen pullara dagitilir.
    /// </summary>
    public class DiceRoll
    {
        private readonly List<int> _moves = new List<int>();

        public int First { get; private set; }
        public int Second { get; private set; }
        public bool IsDouble => First == Second;

        /// <summary>Kartografin Hilesi: toplam puani serbestce bolme modu.</summary>
        public bool FreeSplit { get; set; }

        /// <summary>FreeSplit modunda kalan toplam puan.</summary>
        public int FreeTotal { get; private set; }

        /// <summary>Normal modda kalan hamle degerleri.</summary>
        public IReadOnlyList<int> RemainingMoves => _moves;

        public bool HasMovesLeft => FreeSplit ? FreeTotal > 0 : _moves.Count > 0;

        /// <summary>Zarlari kurar. Cift gelirse klasik kurala gore 4 hamle hakki dogar.</summary>
        public void Set(int first, int second)
        {
            First = first;
            Second = second;

            _moves.Clear();
            _moves.Add(first);
            _moves.Add(second);

            if (first == second)
            {
                _moves.Add(first);
                _moves.Add(second);
            }

            FreeTotal = first + second;
        }

        /// <summary>Bir zar degerini elle degistirir (Meridyen Kaymasi / Sabitleyici Pul kullanir).</summary>
        public void OverrideDie(bool overrideFirst, int value)
        {
            if (overrideFirst)
                Set(value, Second);
            else
                Set(First, value);
        }

        /// <summary>Verilen mesafe icin hamle hakki var mi? (Saf sorgu, harcama yapmaz.)</summary>
        public bool CanAfford(int distance)
        {
            if (distance <= 0)
                return false;

            return FreeSplit ? distance <= FreeTotal : _moves.Contains(distance);
        }

        /// <summary>Hamle hakkini harcar. Hak yoksa false doner ve hicbir sey degismez.</summary>
        public bool Consume(int distance)
        {
            if (!CanAfford(distance))
                return false;

            if (FreeSplit)
                FreeTotal -= distance;
            else
                _moves.Remove(distance);

            return true;
        }

        /// <summary>Gele Pulu: ekstra hamle hakki ekler.</summary>
        public void GrantExtraMove(int value)
        {
            _moves.Add(value);
            FreeTotal += value;
        }

        /// <summary>Gele Pulu'nun kotu sonucu: o eldeki kalan tum hamle haklari yanar.</summary>
        public void LoseRemainingMoves()
        {
            _moves.Clear();
            FreeTotal = 0;
        }

        /// <summary>
        /// Zar durumunun bagimsiz bir kopyasi. Yapay zeka "su hamleyi yaparsam ne olur?"
        /// denemesi yaparken gercek zarlari harcamamali — klon tahtaya bu kopya baglanir.
        /// </summary>
        public DiceRoll Clone()
        {
            DiceRoll copy = new DiceRoll();

            copy.First = First;
            copy.Second = Second;
            copy.FreeSplit = FreeSplit;
            copy.FreeTotal = FreeTotal;
            copy._moves.AddRange(_moves);

            return copy;
        }
    }
}
