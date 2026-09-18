using Tavla.Core.Board;

namespace Tavla.Core.Runtime
{
    /// <summary>
    /// Klon tahtaya baglanan SAHTE context. Tek gercek isi zarlari tasimak;
    /// diger tum yazma cagrilarini sessizce yutar.
    ///
    /// Neden gerekli? GameState zar sorgularini Context.CurrentRoll uzerinden yapiyor.
    /// Klona context baglanmazsa "zar sistemi yok" moduna dusup 1-6 arasi her mesafeyi
    /// gecerli sayardi — bot elinde olmayan zarlarla hamle planlardi.
    ///
    /// Skor/altin/sira cagrilari bos geciyor cunku simulasyon gercek maci ETKILEMEMELI.
    /// Botun degerlendirmesi zaten kendi sezgisel puanini kullaniyor, MatchContext'i degil.
    /// </summary>
    public class SimulationContext : IMatchContext
    {
        public GameState Board { get; private set; }
        public IRandomProvider Random { get; private set; }
        public DiceRoll CurrentRoll { get; private set; }

        /// <param name="board">Klon tahta.</param>
        /// <param name="source">Gercek context; zar durumu buradan KOPYALANIR.</param>
        public SimulationContext(GameState board, IMatchContext source)
        {
            Board = board;

            // Gercek RNG'yi paylasmiyoruz: simulasyon onu ilerletirse asil macin
            // tohumlu (deterministik) akisi bozulurdu.
            Random = new SystemRandomProvider(0);

            if (source != null && source.CurrentRoll != null)
                CurrentRoll = source.CurrentRoll.Clone();
        }

        // --- Yazma cagrilari: simulasyonda hepsi etkisiz ---

        public int AddScore(int playerId, int amount) => 0;

        public int GetScore(int playerId) => 0;

        public void AddScoreMultiplier(int playerId, float multiplier, bool permanent) { }

        public void ResetTurnScore(int playerId) { }

        public void AddGold(int playerId, int amount) { }

        public void AddShopDiscount(int amount) { }

        public void TriggerLuxuryShop(int playerId) { }

        public void SkipTurn(int playerId) { }

        public void PinNextDie(int playerId, int value) { }
    }
}
