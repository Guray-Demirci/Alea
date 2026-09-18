using Tavla.Core.Board;
using Tavla.Core.Checkers;
using Tavla.Core.Interfaces;
using Tavla.Core.Runtime;

namespace Tavla.Core.Tiles
{
    /// <summary>
    /// Ozel karolarin ortak iskeleti. TUM hook'lar "hicbir sey yapma" varsayilaniyla gelir,
    /// boylece her karo sadece ilgilendigi hook'u override eder (ISP).
    /// Orn: Sisli Hane sadece Landed/Left'i, Zindan sadece izin sorgularini ezer.
    /// </summary>
    public abstract class BaseTileEffect : ITileEffect
    {
        public abstract string EffectId { get; }

        /// <summary>Harita karolari tarafsizdir (-1); oyuncu tarafindan kurulan tuzaklar sahibini tasir.</summary>
        public int OwnerId { get; private set; }

        /// <summary>Kalici karolar icin -1. Sureli karolar override eder.</summary>
        public virtual int RemainingTurns => -1;

        public virtual bool IsExpired => false;

        public virtual bool CanLandOn(BaseChecker checker) => true;

        public virtual bool CanLeaveFrom(BaseChecker checker) => true;

        public virtual bool AllowsBlockade => true;

        public virtual void OnCheckerLanded(IMatchContext context, Node node, BaseChecker checker) { }

        public virtual void OnCheckerLeft(IMatchContext context, Node node, BaseChecker checker) { }

        public virtual void OnCheckerPassedOver(IMatchContext context, Node node, BaseChecker checker) { }

        public virtual void OnTurnPassed(IMatchContext context, Node node) { }

        protected BaseTileEffect(int ownerId = -1)
        {
            OwnerId = ownerId;
        }

        /// <summary>Yardimci: verilen pulun rakibinin kimligi (kirma islemlerinde "kiran taraf" olarak kullanilir).</summary>
        protected static int OpponentOf(BaseChecker checker)
        {
            return (checker.OwnerId + 1) % GameState.PlayerCount;
        }
    }
}
