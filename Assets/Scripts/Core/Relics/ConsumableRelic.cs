using Tavla.Core.Interfaces;
using Tavla.Core.Runtime;

namespace Tavla.Core.Relics
{
    /// <summary>
    /// Common skeleton of SINGLE-USE relics. The charge counter and the "armed" state are written
    /// here ONCE — every new single-use relic inherits them instead of re-implementing its own armed flag.
    ///
    /// Lifecycle:
    ///   TryActivate() -> a charge is spent, IsArmed becomes true
    ///   ModifyRoll()  -> the effect is applied, the subclass calls Disarm()
    ///   IsDepleted    -> charges are gone, the item is removed from the inventory
    /// </summary>
    public abstract class ConsumableRelic : BaseRelic, IConsumable
    {
        private readonly ChargePool _charges;

        /// <summary>Is it armed for this turn? Subclasses apply their effect based on this.</summary>
        protected bool IsArmed { get; private set; }

        protected ConsumableRelic(int maxCharges = 1)
        {
            _charges = new ChargePool(maxCharges);
        }

        // --- IConsumable ---
        public int MaxCharges => _charges.Max;
        public int RemainingCharges => _charges.Remaining;
        public bool IsDepleted => _charges.IsEmpty;

        /// <summary>Default: no choice needed. Meridian Shift overrides this.</summary>
        public virtual bool RequiresChoice => false;

        public bool TryActivate(IMatchContext context, int choice)
        {
            if (!_charges.TryConsume())
                return false;

            IsArmed = true;
            OnActivated(context, choice);
            return true;
        }

        public void Refill()
        {
            _charges.Refill();
        }

        // --- IDiceModifier ---
        /// <summary>The dice system sees this relic ONLY during the turn it was armed.</summary>
        public override bool IsActive => IsArmed;

        /// <summary>Runs the moment it is armed. Relics that need a choice store the value here.</summary>
        protected virtual void OnActivated(IMatchContext context, int choice) { }

        /// <summary>Called by the subclass after the effect is applied; the relic goes quiet.</summary>
        protected void Disarm()
        {
            IsArmed = false;
        }
    }
}
