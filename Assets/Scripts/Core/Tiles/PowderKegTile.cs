using System.Collections.Generic;
using Tavla.Core.Abilities;
using Tavla.Core.Board;
using Tavla.Core.Checkers;
using Tavla.Core.Interfaces;
using Tavla.Core.Runtime;

namespace Tavla.Core.Tiles
{
    /// <summary>
    /// Powder Keg Tile (minefield, the red point). "This point keeps counting down from three during the match, and
    /// when the count runs out it blows up every checker on it. If the match is not over, the count starts again."
    ///
    /// The POINT counts, not a checker on it, so the count runs whether or not anyone stands there, and drops at the
    /// end of every turn, either player's. It shows 3 at the start of the match, blows up at the end of the third turn
    /// and shows 3 again. A blown-up checker is destroyed PERMANENTLY, as the keg's first design said: not even the
    /// bar; in a run it leaves the deck too (<see cref="GameState.DestroyChecker"/>). A checker immune to tiles
    /// (Reckless) shrugs the blast off.
    /// </summary>
    public class PowderKegTile : BaseTileEffect, ICountdownTile
    {
        public const string Id = "tile_powderkeg";

        /// <summary>Turns from a fresh count to the blast.</summary>
        public const int FuseTurns = 3;

        // Reused at every blast to collect the victims without allocating a new list.
        private readonly List<BaseChecker> _victims = new List<BaseChecker>();

        public override string EffectId => Id;

        /// <summary>Turn ends left before the blast: 3, 2, 1. The View shows it on the point.</summary>
        public int Countdown { get; private set; } = FuseTurns;

        public override void OnTurnPassed(IMatchContext context, Node node, int endingPlayerId)
        {
            Countdown--;
            if (Countdown > 0)
                return;

            Countdown = FuseTurns;
            Explode(context, node);
        }

        private void Explode(IMatchContext context, Node node)
        {
            context.Board.ReportAbility(AbilityIds.KegExploded, null, node.Index);

            // The stack cannot change while it is walked -> collect the victims first.
            _victims.Clear();
            for (int i = 0; i < node.CheckerCount; i++)
            {
                if (node.Checkers[i].IsAffectedByTiles)
                    _victims.Add(node.Checkers[i]);
            }

            // A checker's loss can end the match; nothing may change on the board after that.
            for (int i = 0; i < _victims.Count && !context.Board.IsMatchOver; i++)
                context.Board.DestroyChecker(_victims[i], node.Index);

            _victims.Clear();
        }
    }
}
