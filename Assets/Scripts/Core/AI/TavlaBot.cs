using System.Collections.Generic;
using Tavla.Core.Board;

namespace Tavla.Core.AI
{
    /// <summary>
    /// Tek hamle derinlikli (greedy) yapay zeka.
    ///
    /// Calisma sekli: her aday hamle icin tahtanin bir KLONUNU alir, hamleyi klonda
    /// oynar, sonucu puanlar ve en yuksek puanliyi secer. Klon gercek tahtayi
    /// etkilemez cunku GameState.IsSimulation bayragi yan etkili kancalari kapatir.
    ///
    /// Kural bilgisi burada YOKTUR. "Bu hamle gecerli mi?" sorusunu hep Core'a
    /// soruyor (GetLegalTargets / CanBearOff / TryMove) — bot da View gibi
    /// kurallarin tuketicisi, sahibi degil.
    /// </summary>
    public class TavlaBot
    {
        #region Kimlik

        private readonly int _myPlayerId;
        private readonly int _opponentId;

        public int PlayerId => _myPlayerId;

        public TavlaBot(int myPlayerId)
        {
            _myPlayerId = myPlayerId;
            _opponentId = myPlayerId == 0 ? 1 : 0;
        }

        #endregion

        #region Agirliklar

        // Sezgisel puanin ayar dugmeleri. Botu sertlestirmek/yumusatmak icin tek yer burasi.
        private const int BarPenalty        = 10000; // Kendi pulumuz kirildi — felaket
        private const int BarReward         = 1000;  // Rakibi kirdik
        private const int BorneOffReward    = 5000;  // Pul topladik — nihai hedef
        private const int BlotPenalty       = 300;   // Acik verdik, kirilabiliriz
        private const int PointReward       = 200;   // Kapi aldik, guvendeyiz
        private const int ProgressPerStep   = 10;    // Eve yaklasma

        #endregion

        #region Degerlendirme

        /// <summary>
        /// Yapay zekanin beyni. Verilen tahta manzarasina bakar ve bir puan bicer.
        /// Puan ne kadar yuksekse, tahtanin o anki durumu AI icin o kadar iyidir.
        /// </summary>
        public int EvaluateBoard(GameState virtualBoard)
        {
            int score = 0;

            // ---------- 1) KIRMA VE KIRILMA ----------
            score -= virtualBoard.GetBarCount(_myPlayerId) * BarPenalty;
            score += virtualBoard.GetBarCount(_opponentId) * BarReward;

            // ---------- 2) TOPLANAN PULLAR (Nihai hedef) ----------
            // Rakibin topladiklari da hesaba katiliyor: arcade modunda kazanan skora gore
            // belirlendigi icin rakip 14 pul toplarken oturup seyretmek olmaz.
            score += virtualBoard.GetBorneOffCount(_myPlayerId) * BorneOffReward;
            score -= virtualBoard.GetBorneOffCount(_opponentId) * BorneOffReward;

            // ---------- 3) TAHTA DURUMU (Aciklar, kapilar, ilerleme) ----------
            for (int i = 0; i < GameState.NodeCount; i++)
            {
                Node node = virtualBoard.Nodes[i];

                // DIKKAT: "node.Checkers[0].OwnerId == benim" DEGIL.
                // Parazit Pul rakip hanesine sizabildigi icin bir hanede iki oyuncunun
                // pulu birden bulunabiliyor; sayim oyuncu bazli yapilmak zorunda.
                int myCount = node.CountFor(_myPlayerId);
                if (myCount == 0)
                    continue;

                if (myCount == 1)
                    score -= BlotPenalty;   // Acik (blot)
                else
                    score += PointReward;   // Kapi (blokaj)

                // Ilerleme: 1 = bitise en yakin, 24 = en uzak.
                int distanceToFinish = GameState.GetBearOffDistance(i, _myPlayerId);

                // Hanedeki pul SAYISIYLA carpiliyor: 5 pullu bir hanenin ilerlemesi
                // 1 pullu haneyle ayni sayilirsa bot yiginlari ileri tasimayi hic istemez.
                score += (GameState.NodeCount - distanceToFinish) * ProgressPerStep * myCount;
            }

            return score;
        }

        #endregion

        #region Hamle secimi

        /// <summary>
        /// Tum gecerli hamleleri deneyip en yuksek puanliyi secer.
        /// Tahtayi DEGISTIRMEZ — denemeler klon uzerinde yapilir.
        /// </summary>
        public BotMove ChooseBestMove(GameState board)
        {
            if (board == null || board.IsMatchOver || board.CurrentPlayerId != _myPlayerId)
                return BotMove.None;

            BotMove best = BotMove.None;
            int bestScore = int.MinValue;

            List<int> sources = CollectSources(board);

            for (int s = 0; s < sources.Count; s++)
            {
                int from = sources[s];

                // --- Tahta ustundeki hedefler ---
                List<int> targets = board.GetLegalTargets(from);
                for (int t = 0; t < targets.Count; t++)
                {
                    int value;
                    if (!TrySimulate(board, from, targets[t], out value))
                        continue;

                    if (value > bestScore)
                    {
                        bestScore = value;
                        best = new BotMove(from, targets[t], value);
                    }
                }

                // --- Toplama (bear-off) ---
                // GetLegalTargets sadece 0-23 hanelerini tariyor; toplama ayri sorulmali.
                if (board.CanBearOff(from))
                {
                    int value;
                    if (TrySimulate(board, from, GameState.BearOffIndex, out value) && value > bestScore)
                    {
                        bestScore = value;
                        best = new BotMove(from, GameState.BearOffIndex, value);
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// En iyi hamleyi GERCEK tahtada oynar. Oynanacak hamle kalmadiysa false doner —
        /// cagiran taraf bunu "turu bitir" sinyali olarak kullanir.
        /// </summary>
        public bool PlayOneMove(GameState board)
        {
            BotMove move = ChooseBestMove(board);
            if (!move.IsValid)
                return false;

            return board.TryMove(move.FromIndex, move.ToIndex);
        }

        #endregion

        #region Yardimcilar

        /// <summary>
        /// Hamlenin oynanabilecegi kaynak haneler.
        /// Kirik pul varsa kural geregi baska hicbir hamle yapilamaz — tek kaynak bardir.
        /// </summary>
        private List<int> CollectSources(GameState board)
        {
            List<int> sources = new List<int>();

            if (board.GetBarCount(_myPlayerId) > 0)
            {
                sources.Add(GameState.BarIndex);
                return sources;
            }

            for (int i = 0; i < GameState.NodeCount; i++)
            {
                if (board.Nodes[i].CountFor(_myPlayerId) > 0)
                    sources.Add(i);
            }

            return sources;
        }

        /// <summary>
        /// Hamleyi bir klonda oynayip sonucu puanlar. Klonda tutmazsa false doner.
        /// Gercek tahtaya, gercek zarlara ve gercek pullarin ic durumuna dokunmaz.
        /// </summary>
        private bool TrySimulate(GameState board, int from, int to, out int value)
        {
            value = int.MinValue;

            GameState virtualBoard = board.Clone();
            if (!virtualBoard.TryMove(from, to))
                return false;

            value = EvaluateBoard(virtualBoard);
            return true;
        }

        #endregion
    }
}
