using Tavla.Core.Interfaces;

namespace Tavla.Core.Relics
{
    /// <summary>
    /// Common skeleton of PERMANENT relics. Bought once, active on every roll for the whole match.
    /// There is no charge counter, so it shares no code with <see cref="ConsumableRelic"/> —
    /// the two branches are deliberately kept apart.
    /// </summary>
    public abstract class PermanentRelic : BaseRelic, IPermanent
    {
        /// <summary>A permanent relic is always active.</summary>
        public override bool IsActive => true;
    }
}
