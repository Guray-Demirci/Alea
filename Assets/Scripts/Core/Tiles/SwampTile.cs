using System.Collections.Generic;
using Tavla.Core.Abilities;
using Tavla.Core.Board;
using Tavla.Core.Checkers;
using Tavla.Core.Runtime;

namespace Tavla.Core.Tiles
{
    /// <summary>
    /// Swamp Tile. "Every checker that lands here has a 50% chance to get stuck (locked).
    /// Unlocking it requires sacrificing one of the dice on a later turn."
    /// The lock is kept PER CHECKER: one checker on the point may be stuck while another is free.
    /// </summary>
    public class SwampTile : BaseTileEffect
    {
        public const string Id = "tile_swamp";
        private const float StickChance = 0.5f;

        private readonly HashSet<BaseChecker> _stuck = new HashSet<BaseChecker>();

        public override string EffectId => Id;

        public override bool CanLeaveFrom(BaseChecker checker) => !_stuck.Contains(checker);

        public bool IsStuck(BaseChecker checker) => _stuck.Contains(checker);

        public override void OnCheckerLanded(IMatchContext context, Node node, BaseChecker checker)
        {
            if (!context.Random.Chance(StickChance))
                return;

            _stuck.Add(checker);
            context.Board.ReportAbility(AbilityIds.SwampStuck, checker, node.Index);
        }

        public override void OnCheckerLeft(IMatchContext context, Node node, BaseChecker checker)
        {
            // Once the checker has left the point the lock record is no longer needed (also prevents leaks).
            _stuck.Remove(checker);
        }

        /// <summary>
        /// The "sacrifice a die to unlock" action. It is NOT a move, so it does not go through TryMove;
        /// it is called when the player chooses "free the checker" in the UI and spends the chosen die.
        /// </summary>
        public bool TryUnlock(IMatchContext context, BaseChecker checker, int sacrificedDieValue)
        {
            if (!_stuck.Contains(checker))
                return false;

            if (context.CurrentRoll != null && !context.CurrentRoll.Consume(sacrificedDieValue))
                return false;

            _stuck.Remove(checker);
            context.Board.ReportAbility(AbilityIds.SwampReleased, checker, context.Board.FindNodeIndexOf(checker));
            return true;
        }
    }
}
