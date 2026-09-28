using Tavla.Core.Board;
using Tavla.Core.Checkers;
using Tavla.Core.Interfaces;
using Tavla.Core.Runtime;

namespace Tavla.Core.Tiles
{
    /// <summary>
    /// Common skeleton of special tiles. EVERY hook comes with a "do nothing" default,
    /// so each tile overrides only the hooks it cares about (ISP).
    /// E.g. the Fog Tile only overrides Landed/Left, the Dungeon only the permission queries.
    /// </summary>
    public abstract class BaseTileEffect : ITileEffect
    {
        /// <summary>Owner id used by neutral map tiles.</summary>
        public const int NeutralOwner = -1;

        /// <summary>Remaining-turn value reported by tiles that never expire.</summary>
        public const int Permanent = -1;

        public abstract string EffectId { get; }

        /// <summary>Map tiles are neutral (<see cref="NeutralOwner"/>); traps placed by a player carry their owner.</summary>
        public int OwnerId { get; private set; }

        /// <summary><see cref="Permanent"/> for permanent tiles. Timed tiles override this.</summary>
        public virtual int RemainingTurns => Permanent;

        public virtual bool IsExpired => false;

        public virtual bool CanLandOn(BaseChecker checker) => true;

        public virtual bool CanLeaveFrom(BaseChecker checker) => true;

        public virtual bool AllowsBlockade => true;

        public virtual void OnCheckerLanded(IMatchContext context, Node node, BaseChecker checker) { }

        public virtual void OnCheckerLeft(IMatchContext context, Node node, BaseChecker checker) { }

        public virtual void OnCheckerPassedOver(IMatchContext context, Node node, BaseChecker checker) { }

        public virtual void OnTurnPassed(IMatchContext context, Node node) { }

        protected BaseTileEffect(int ownerId = NeutralOwner)
        {
            OwnerId = ownerId;
        }

        /// <summary>Helper: id of the given checker's opponent (used as the "capturing side" when a tile captures).</summary>
        protected static int OpponentOf(BaseChecker checker)
        {
            return (checker.OwnerId + 1) % GameState.PlayerCount;
        }
    }
}
