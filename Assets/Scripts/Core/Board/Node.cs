using System.Collections.Generic;
using Tavla.Core.Abilities;
using Tavla.Core.Checkers;
using Tavla.Core.Interfaces;

namespace Tavla.Core.Board
{
    /// <summary>
    /// Tahtadaki tek bir hane. Pullari LIFO (yigin) mantigiyla tutar.
    ///
    /// ONEMLI DEGISIKLIK: Parazit Pul rakip kapisina sizabildigi icin bir hane artik
    /// AYNI ANDA IKI OYUNCUNUN pulunu barindirabilir. Bu yuzden "OwnerId" tek basina
    /// yeterli degil; kural hesaplari oyuncu bazli sayimlar uzerinden yapilir.
    /// </summary>
    public class Node
    {
        public const int EmptyOwner = -1;

        public int Index { get; private set; }

        private readonly List<BaseChecker> _checkers = new List<BaseChecker>();

        public IReadOnlyList<BaseChecker> Checkers => _checkers;

        public int StoneCount => _checkers.Count;

        /// <summary>
        /// Haneyi "tutan" oyuncu = en alttaki pulun sahibi.
        /// Parazit sizsa bile hanenin asil sahibi degismez; sadece bir misafir vardir.
        /// </summary>
        public int OwnerId => _checkers.Count == 0 ? EmptyOwner : _checkers[0].OwnerId;

        /// <summary>Bu hanede kurulu ozel karo (yoksa null).</summary>
        public ITileEffect ActiveTile { get; private set; }

        public BaseChecker TopChecker => _checkers.Count == 0 ? null : _checkers[_checkers.Count - 1];

        public bool IsEmpty => _checkers.Count == 0;

        public Node(int index)
        {
            Index = index;
        }

        #region Sayim / sorgu

        /// <summary>Verilen oyuncunun bu hanedeki pul sayisi.</summary>
        public int CountFor(int playerId)
        {
            int count = 0;
            for (int i = 0; i < _checkers.Count; i++)
            {
                if (_checkers[i].OwnerId == playerId)
                    count++;
            }
            return count;
        }

        /// <summary>Verilen oyuncuya gore dusman pul sayisi.</summary>
        public int CountEnemiesOf(int playerId) => _checkers.Count - CountFor(playerId);

        /// <summary>Verilen oyuncuya gore en ustteki dusman pul (kirma hedefi). Yoksa null.</summary>
        public BaseChecker GetTopEnemyOf(int playerId)
        {
            for (int i = _checkers.Count - 1; i >= 0; i--)
            {
                if (_checkers[i].OwnerId != playerId)
                    return _checkers[i];
            }
            return null;
        }

        /// <summary>Verilen oyuncunun bu hanedeki tum dusman pullarini toplar (Kiyamet Pulu icin).</summary>
        public List<BaseChecker> GetEnemiesOf(int playerId)
        {
            List<BaseChecker> enemies = new List<BaseChecker>();
            for (int i = 0; i < _checkers.Count; i++)
            {
                if (_checkers[i].OwnerId != playerId)
                    enemies.Add(_checkers[i]);
            }
            return enemies;
        }

        /// <summary>
        /// Bu hane verilen oyuncuya kapali mi?
        /// Uc sebep: (1) rakibin 2+ pulu var, (2) Zirhli Pul tek basina kapi sayiyor,
        /// (3) tek rakip pulu kirilamaz durumda.
        /// Yetenekler GetAbility ile sorgulandigi icin Sis icindeyken hepsi otomatik devre disi kalir.
        /// </summary>
        public bool IsBlockedFor(int playerId)
        {
            int enemyCount = CountEnemiesOf(playerId);
            if (enemyCount == 0)
                return false;

            // Zindan karosu kapaliyken hanede kapi kurulamaz -> blokaj da olusmaz.
            bool blockadeAllowed = ActiveTile == null || ActiveTile.AllowsBlockade;

            if (enemyCount >= 2)
                return blockadeAllowed;

            BaseChecker enemy = GetTopEnemyOf(playerId);

            IBlockadeOverride solo = enemy.GetAbility<IBlockadeOverride>();
            if (solo != null && solo.FormsBlockadeAlone && blockadeAllowed)
                return true;

            // Tek pul ama kirilamiyorsa (Zirhli) yine gecilemez.
            return !enemy.CanBeCaptured;
        }

        /// <summary>
        /// Hanenin gecerli pul kapasitesi. Kara Delik Pulu bu siniri kaldirir.
        /// Hanedeki HERHANGI bir pulun yetenegi limiti gevsetebilir.
        /// </summary>
        public int GetStackLimit(int defaultLimit)
        {
            int limit = defaultLimit;

            for (int i = 0; i < _checkers.Count; i++)
            {
                IStackLimitOverride ovr = _checkers[i].GetAbility<IStackLimitOverride>();
                if (ovr == null)
                    continue;

                int candidate = ovr.GetStackLimit(defaultLimit);
                if (candidate > limit)
                    limit = candidate;
            }

            return limit;
        }

        #endregion

        #region Degistiriciler

        public void Push(BaseChecker checker)
        {
            _checkers.Add(checker);
        }

        public BaseChecker Pop()
        {
            if (_checkers.Count == 0)
                return null;

            BaseChecker top = _checkers[_checkers.Count - 1];
            _checkers.RemoveAt(_checkers.Count - 1);
            return top;
        }

        /// <summary>Belirli bir pulu haneden cikarir (Kiyamet, Kazik, Girdap gibi hedefli etkiler icin).</summary>
        public bool Remove(BaseChecker checker)
        {
            return _checkers.Remove(checker);
        }

        public void SetTile(ITileEffect effect)
        {
            ActiveTile = effect;
        }

        public void ClearTile()
        {
            ActiveTile = null;
        }

        #endregion
    }
}
