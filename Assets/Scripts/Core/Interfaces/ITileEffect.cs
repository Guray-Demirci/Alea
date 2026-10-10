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
    ///   OnTurnPassed         -> Powder Keg (the owner's countdown), Swamp, Plunder Cove
    ///   OnRoundPassed        -> Dungeon (open/close — per round, so both players see both states)
    ///
    /// Optional capabilities live in their own interfaces so tiles without them carry no extra members:
    ///   <see cref="IReleasableLock"/> -> Swamp (a die can be sacrificed to free a stuck checker)
    ///
    /// A checker immune to tiles (Reckless) never reaches the per-checker hooks: GameState filters it out through
    /// <see cref="BaseChecker.IsAffectedByTiles"/>. Only a hook that walks the point's checkers itself
    /// (Plunder Cove's OnTurnPassed) has to ask.
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

        /// <summary>Called at the end of every turn. Countdowns run here.</summary>
        /// <param name="endingPlayerId">The player whose turn has just ended.</param>
        void OnTurnPassed(IMatchContext context, Node node, int endingPlayerId);

        /// <summary>
        /// Called when a full round has ended, i.e. the turn returns to a player who has already played in it —
        /// normally once both players have moved. Cycles that every player must see run here (Dungeon).
        /// </summary>
        void OnRoundPassed(IMatchContext context, Node node);
    }
}
