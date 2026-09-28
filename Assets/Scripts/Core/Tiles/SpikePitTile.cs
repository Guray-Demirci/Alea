using Tavla.Core.Abilities;
using Tavla.Core.Board;
using Tavla.Core.Checkers;
using Tavla.Core.Runtime;

namespace Tavla.Core.Tiles
{
    /// <summary>
    /// Spike Pit Tile. "Every checker that lands here is instantly captured and kicked to the bar.
    /// Only special checkers such as the Armored Checker can pass unharmed — they break the spikes
    /// and turn the point back into a normal one."
    /// We query the ABILITY, not the concrete class: any checker that gains immunity later can pass automatically.
    /// </summary>
    public class SpikePitTile : BaseTileEffect
    {
        public const string Id = "tile_spikepit";

        private bool _broken;

        public override string EffectId => Id;

        /// <summary>Once the spikes are broken the tile has expired; GameState removes it from the point.</summary>
        public override bool IsExpired => _broken;

        public override void OnCheckerLanded(IMatchContext context, Node node, BaseChecker checker)
        {
            ICaptureImmunity immunity = checker.GetAbility<ICaptureImmunity>();

            if (immunity != null && immunity.IsImmune)
            {
                _broken = true;
                context.Board.ReportAbility(AbilityIds.SpikeBroken, checker, node.Index);
                return;
            }

            context.Board.ReportAbility(AbilityIds.SpikeHit, checker, node.Index);
            context.Board.ForceCapture(checker, node.Index, OpponentOf(checker));
        }
    }
}
