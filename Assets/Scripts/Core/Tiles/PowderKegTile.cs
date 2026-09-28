using System.Collections.Generic;
using Tavla.Core.Abilities;
using Tavla.Core.Board;
using Tavla.Core.Checkers;
using Tavla.Core.Runtime;

namespace Tavla.Core.Tiles
{
    /// <summary>
    /// Powder Keg Tile (minefield). "When a checker steps here a 3-turn countdown starts.
    /// If you do not move the checker away within 3 turns the keg explodes and the checker is destroyed
    /// PERMANENTLY (it does not even go to the bar)."
    /// The countdown is per checker; every checker on the point has its own fuse.
    /// </summary>
    public class PowderKegTile : BaseTileEffect
    {
        public const string Id = "tile_powderkeg";
        private const int FuseTurns = 3;

        /// <summary>Returned by <see cref="GetRemainingFuse"/> when the checker has no lit fuse.</summary>
        public const int NoFuse = -1;

        private readonly Dictionary<BaseChecker, int> _fuses = new Dictionary<BaseChecker, int>();

        // Reused every turn to collect fuse keys without allocating a new list.
        private readonly List<BaseChecker> _fuseKeys = new List<BaseChecker>();

        public override string EffectId => Id;

        /// <summary>Remaining turns, so the View can show counter pixels on the tile. <see cref="NoFuse"/> if none.</summary>
        public int GetRemainingFuse(BaseChecker checker)
        {
            int turns;
            return _fuses.TryGetValue(checker, out turns) ? turns : NoFuse;
        }

        public override void OnCheckerLanded(IMatchContext context, Node node, BaseChecker checker)
        {
            _fuses[checker] = FuseTurns;
            context.Board.ReportAbility(AbilityIds.FuseLit, checker, node.Index);
        }

        public override void OnCheckerLeft(IMatchContext context, Node node, BaseChecker checker)
        {
            // The checker escaped in time: the countdown is cancelled.
            if (_fuses.Remove(checker))
                context.Board.ReportAbility(AbilityIds.FuseDefused, checker, node.Index);
        }

        public override void OnTurnPassed(IMatchContext context, Node node)
        {
            if (_fuses.Count == 0)
                return;

            // The dictionary cannot be modified while iterating -> collect the detonations first.
            List<BaseChecker> detonated = null;
            _fuseKeys.Clear();
            _fuseKeys.AddRange(_fuses.Keys);

            for (int i = 0; i < _fuseKeys.Count; i++)
            {
                BaseChecker checker = _fuseKeys[i];
                int remaining = _fuses[checker] - 1;

                if (remaining > 0)
                {
                    _fuses[checker] = remaining;
                    continue;
                }

                if (detonated == null)
                    detonated = new List<BaseChecker>();

                detonated.Add(checker);
            }

            if (detonated == null)
                return;

            for (int i = 0; i < detonated.Count; i++)
            {
                _fuses.Remove(detonated[i]);
                context.Board.ReportAbility(AbilityIds.KegExploded, detonated[i], node.Index);

                // Not even the bar: removed from the game entirely.
                context.Board.DestroyChecker(detonated[i], node.Index);
            }
        }
    }
}
