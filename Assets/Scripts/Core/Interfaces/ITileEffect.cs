using Tavla.Core.Board;
using Tavla.Core.Checkers;
using Tavla.Core.Runtime;

namespace Tavla.Core.Interfaces
{
    /// <summary>
    /// Contract for a special tile / trap installed on a point (<see cref="Node"/>).
    /// GameState does NOT know concrete tile classes; it talks to them only through this interface (DIP).
    ///
    /// The hook set was derived from the needs of the 8 designed tiles:
    ///   CanLandOn            -> Dungeon (no entry while closed)
    ///   CanLeaveFrom         -> Swamp (stuck checkers cannot leave), Dungeon (locked turn)
    ///   AllowsBlockade       -> Dungeon (no point can be made while closed)
    ///   OnCheckerLanded      -> Spike Pit, Maelstrom, Swamp, Fog, Toll Booth, Powder Keg
    ///   OnCheckerLeft        -> Fog (restore abilities), Powder Keg (stop the fuse)
    ///   OnCheckerPassedOver  -> Toll Booth ("every checker that passes or sits here")
    ///   OnTurnPassed         -> Dungeon (open/close), Powder Keg (countdown), Plunder Cove
    /// </summary>
    public interface ITileEffect
    {
        /// <summary>Tile type identifier. The View uses it to pick the right visuals/VFX.</summary>
        string EffectId { get; }

        /// <summary>Player who placed the tile; -1 for neutral map tiles.</summary>
        int OwnerId { get; }

        /// <summary>How many more turns the tile stays active. -1 for permanent tiles.</summary>
        int RemainingTurns { get; }

        /// <summary>Has the tile expired? If so, GameState removes it from the point.</summary>
        bool IsExpired { get; }

        /// <summary>Can this checker land on this point? Called during VALIDATION — must have no side effects.</summary>
        bool CanLandOn(BaseChecker checker);

        /// <summary>Can this checker leave this point? (Swamp lock, Dungeon bars.)</summary>
        bool CanLeaveFrom(BaseChecker checker);

        /// <summary>Can a point (blockade) be made here? Returns false while the Dungeon is closed.</summary>
        bool AllowsBlockade { get; }

        /// <summary>AFTER a checker has settled on the point. The tile's main effect is applied here.</summary>
        void OnCheckerLanded(IMatchContext context, Node node, BaseChecker checker);

        /// <summary>AFTER a checker has left the point. Temporary effects are reverted here.</summary>
        void OnCheckerLeft(IMatchContext context, Node node, BaseChecker checker);

        /// <summary>When a checker passes OVER this point without stopping. Used by the Toll Booth.</summary>
        void OnCheckerPassedOver(IMatchContext context, Node node, BaseChecker checker);

        /// <summary>Called at the end of every turn. Countdowns and open/close cycles run here.</summary>
        void OnTurnPassed(IMatchContext context, Node node);
    }
}
