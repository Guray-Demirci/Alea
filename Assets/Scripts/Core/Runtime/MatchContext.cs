using System;
using System.Collections.Generic;
using Tavla.Core.Board;
using Tavla.Core.Relics;

namespace Tavla.Core.Runtime
{
    /// <summary>
    /// IMatchContext'in calisan implementasyonu: skor, altin, carpan, dukkan ve zar durumunu tutar.
    /// Yetenekler bu sinifi degil, ARAYUZUNU gorur — testte sahte bir context takmak serbesttir.
    /// </summary>
    public class MatchContext : IMatchContext
    {
        private readonly int[] _turnScore;
        private readonly int[] _totalScore;
        private readonly int[] _gold;
        private readonly float[] _turnMultiplier;
        private readonly float[] _permanentMultiplier;
        private readonly int[] _pinnedDie;

        private readonly List<IDiceModifier> _diceModifiers = new List<IDiceModifier>();
        private readonly DiceRoller _roller;

        public GameState Board { get; private set; }
        public IRandomProvider Random { get; private set; }
        public DiceRoll CurrentRoll { get; private set; }

        /// <summary>Fedakar Pul'un biriktirdigi kalici dukkan indirimi (altin).</summary>
        public int ShopDiscount { get; private set; }

        /// <summary>Anka Pulu'nun tetikledigi "Kullerinden Dogus" vitrini aktif mi?</summary>
        public bool LuxuryShopPending { get; private set; }

        #region Olaylar (Core -> View)

        public event Action<int, int> OnScoreChanged;   // (playerId, toplam skor)
        public event Action<int, int> OnGoldChanged;    // (playerId, toplam altin)
        public event Action<int, float> OnMultiplierChanged;
        public event Action<int> OnTurnSkipped;
        public event Action<DiceRoll> OnDiceRolled;
        public event Action<int> OnShopDiscountChanged;
        public event Action<int> OnLuxuryShopTriggered;

        #endregion

        public MatchContext(GameState board, IRandomProvider random)
        {
            Board = board;
            Random = random;
            _roller = new DiceRoller(random);

            int players = GameState.PlayerCount;
            _turnScore = new int[players];
            _totalScore = new int[players];
            _gold = new int[players];
            _turnMultiplier = new float[players];
            _permanentMultiplier = new float[players];
            _pinnedDie = new int[players];

            for (int p = 0; p < players; p++)
            {
                _turnMultiplier[p] = 1f;
                _permanentMultiplier[p] = 1f;
            }

            // Iki fazli kurulum: tahtaya kendimizi tanitiyoruz ki yetenekler context'e ulasabilsin.
            Board.Bind(this);
        }

        #region Skor

        public int AddScore(int playerId, int amount)
        {
            // Carpanlar burada uygulanir; pullar sadece HAM puan gonderir (SRP).
            float multiplier = _turnMultiplier[playerId] * _permanentMultiplier[playerId];
            int final = (int)(amount * multiplier);

            _turnScore[playerId] += final;
            _totalScore[playerId] += final;

            OnScoreChanged?.Invoke(playerId, _totalScore[playerId]);
            return final;
        }

        public void AddScoreMultiplier(int playerId, float multiplier, bool permanent)
        {
            if (permanent)
                _permanentMultiplier[playerId] *= multiplier;
            else
                _turnMultiplier[playerId] *= multiplier;

            OnMultiplierChanged?.Invoke(playerId, _turnMultiplier[playerId] * _permanentMultiplier[playerId]);
        }

        /// <summary>Korsan Koyu: o turda kazanilan puani geri alir.</summary>
        public void ResetTurnScore(int playerId)
        {
            _totalScore[playerId] -= _turnScore[playerId];
            _turnScore[playerId] = 0;

            OnScoreChanged?.Invoke(playerId, _totalScore[playerId]);
        }

        public int GetScore(int playerId) => _totalScore[playerId];

        #endregion

        #region Ekonomi

        public void AddGold(int playerId, int amount)
        {
            _gold[playerId] += amount;

            // Haydut Kampi: "altinin eksiye dusmez ama o el kazandigin skordan puan siler."
            if (_gold[playerId] < 0)
            {
                int debt = -_gold[playerId];
                _gold[playerId] = 0;
                _totalScore[playerId] -= debt * 10;
                OnScoreChanged?.Invoke(playerId, _totalScore[playerId]);
            }

            OnGoldChanged?.Invoke(playerId, _gold[playerId]);
        }

        public int GetGold(int playerId) => _gold[playerId];

        public void AddShopDiscount(int amount)
        {
            ShopDiscount += amount;
            OnShopDiscountChanged?.Invoke(ShopDiscount);
        }

        public void TriggerLuxuryShop(int playerId)
        {
            LuxuryShopPending = true;
            OnLuxuryShopTriggered?.Invoke(playerId);
        }

        /// <summary>Dukkan asamasi vitrini kurduktan sonra bayragi temizler.</summary>
        public void ConsumeLuxuryShop()
        {
            LuxuryShopPending = false;
        }

        #endregion

        #region Tur / Zar

        public void SkipTurn(int playerId)
        {
            Board.RequestSkipTurn(playerId);
            OnTurnSkipped?.Invoke(playerId);
        }

        public void PinNextDie(int playerId, int value)
        {
            _pinnedDie[playerId] = value;
        }

        /// <summary>Kalici zar tilsimlarini (Ikiz Ruzgarlar, Firtina Raporu) kaydeder.</summary>
        public void RegisterDiceModifier(IDiceModifier modifier)
        {
            if (modifier != null && !_diceModifiers.Contains(modifier))
                _diceModifiers.Add(modifier);
        }

        public void UnregisterDiceModifier(IDiceModifier modifier)
        {
            _diceModifiers.Remove(modifier);
        }

        /// <summary>
        /// Tur basinda zar atar: once sabitlenmis zar uygulanir, sonra tilsimlar sirayla calisir.
        /// </summary>
        public DiceRoll RollDice(int playerId)
        {
            CurrentRoll = _roller.Roll(_diceModifiers);

            // Sabitleyici Pul / Meridyen Kaymasi: bir zar kesin olarak belirlenen degere oturur.
            if (_pinnedDie[playerId] > 0)
            {
                CurrentRoll.OverrideDie(true, _pinnedDie[playerId]);
                _pinnedDie[playerId] = 0;
            }

            // Turun skor carpani sifirlanir; kalici carpan (Kusatma) korunur.
            _turnMultiplier[playerId] = 1f;
            _turnScore[playerId] = 0;

            OnDiceRolled?.Invoke(CurrentRoll);
            return CurrentRoll;
        }

        #endregion
    }
}
