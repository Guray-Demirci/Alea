using System.Collections.Generic;
using Tavla.Core.Checkers;

namespace Tavla.Core.Board
{
    /// <summary>Hamlenin neden reddedildigini anlatan sebep kodu. View bunu UI mesajina cevirir.</summary>
    public enum MoveError
    {
        None = 0,
        OutOfBounds,        // Hane indeksi tahta disinda
        SourceEmpty,        // Kaynak hane bos
        NotYourChecker,     // Kaynaktaki pul rakibe ait
        WrongDirection,     // Oyuncunun ilerleme yonune ters hamle
        InvalidDistance,    // Mesafe 1-6 araligi disinda
        NoDiceValue,        // Elde bu mesafeyi karsilayacak zar yok
        DestinationBlocked, // Hedef hane rakip tarafindan tutuluyor
        StackFull,          // Hane kapasitesi dolu (varsayilan 5; Kara Delik Pulu bu siniri kaldirir)
        BlockedByTile,      // Hedefteki ozel karo girisi engelledi (Zindan)
        CheckerLocked,      // Pul bulundugu hanede kilitli (Bataklik / Zindan)
        MustEnterFromBar,   // Kirik pulu varken normal hamle yapilamaz

        // --- Arcade modu ---
        MatchOver,          // Tur sayaci bitti, mac kapandi
        NotInHomeBoard,     // Toplama denendi ama pul kendi ev bolgesinde degil
        NoDieForBearOff     // Kalan mesafeyi karsilayacak kadar buyuk zar yok
    }

    /// <summary>
    /// Bir hamle denemesinin sonucu.
    /// Kiyamet Pulu tek hamlede birden fazla pul kirabildigi icin kirilanlar LISTE olarak tutulur.
    ///
    /// ARCADE MODU: Artik puan ve toplama bilgisini de tasiyor. View bu iki alana bakarak
    /// skor animasyonunu oynatir ve toplanan pulun gorselini sahneden kaldirir.
    /// </summary>
    public readonly struct MoveResult
    {
        private static readonly BaseChecker[] NoCaptures = new BaseChecker[0];

        public readonly bool Success;
        public readonly MoveError Error;
        public readonly int FromIndex;
        public readonly int ToIndex;
        public readonly int PlayerId;

        /// <summary>Hareket eden pul (basarisiz hamlede null olabilir).</summary>
        public readonly BaseChecker MovedChecker;

        /// <summary>Bu hamlede kirilan rakip pullar. Kirilma yoksa bos dizi (asla null degil).</summary>
        public readonly IReadOnlyList<BaseChecker> CapturedCheckers;

        /// <summary>Parazit Pul rakip kapisina sizdiysa true — View farkli bir animasyon oynatir.</summary>
        public readonly bool WasInfiltration;

        /// <summary>
        /// Bu hamle bir TOPLAMA (bear-off) miydi? True ise pul tahtadan tamamen cikti;
        /// View onun GameObject'ini yok etmeli. ToIndex bu durumda GameState.BearOffIndex olur.
        /// </summary>
        public readonly bool WasBearOff;

        /// <summary>
        /// Bu hamlenin oyuncuya yazdirdigi puan — CARPANLAR UYGULANDIKTAN SONRAKI deger.
        /// Kirma ve toplama puanlarinin toplamidir. Kesin skor toplami icin
        /// MatchContext.OnScoreChanged dinlenmelidir; buradaki deger "bu hamle ne kazandirdi"dir.
        /// </summary>
        public readonly int ScoreGained;

        public bool HasCapture => CapturedCheckers.Count > 0;

        private MoveResult(bool success, MoveError error, int from, int to, int playerId,
                           BaseChecker moved, IReadOnlyList<BaseChecker> captured,
                           bool infiltration, bool bearOff, int scoreGained)
        {
            Success = success;
            Error = error;
            FromIndex = from;
            ToIndex = to;
            PlayerId = playerId;
            MovedChecker = moved;
            CapturedCheckers = captured ?? NoCaptures;
            WasInfiltration = infiltration;
            WasBearOff = bearOff;
            ScoreGained = scoreGained;
        }

        /// <summary>Normal (tahta uzerinde) basarili hamle.</summary>
        public static MoveResult Ok(int from, int to, int playerId, BaseChecker moved,
                                    IReadOnlyList<BaseChecker> captured, bool infiltration,
                                    int scoreGained)
            => new MoveResult(true, MoveError.None, from, to, playerId, moved, captured,
                              infiltration, false, scoreGained);

        /// <summary>
        /// Basarili toplama. Hedef bir hane olmadigi icin ToIndex sentinel deger alir
        /// ve kirilan pul listesi her zaman bostur.
        /// </summary>
        public static MoveResult BearOff(int from, int toSentinel, int playerId,
                                         BaseChecker moved, int scoreGained)
            => new MoveResult(true, MoveError.None, from, toSentinel, playerId, moved, NoCaptures,
                              false, true, scoreGained);

        public static MoveResult Fail(MoveError error, int from, int to, int playerId)
            => new MoveResult(false, error, from, to, playerId, null, NoCaptures, false, false, 0);
    }
}
