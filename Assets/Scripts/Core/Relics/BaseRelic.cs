using System.Collections.Generic;
using Tavla.Core.Interfaces;
using Tavla.Core.Runtime;

namespace Tavla.Core.Relics
{
    /// <summary>
    /// Common skeleton of dice relics. Same philosophy as BaseChecker for checkers:
    /// kept thin, behaviour is defined in subclasses. Default hooks do nothing,
    /// so each relic overrides only the hooks it cares about (ISP).
    ///
    /// NOTE: this class deliberately implements only <see cref="IPurchasable"/> — neither
    /// <see cref="IConsumable"/> nor <see cref="IPermanent"/>. The branch is chosen one level down
    /// (<see cref="ConsumableRelic"/> / <see cref="PermanentRelic"/>). Because it is abstract, a relic with
    /// an "undecided" branch can never be instantiated; every concrete relic MUST fall into one of the two.
    /// </summary>
    public abstract class BaseRelic : IPurchasable, IDiceModifier
    {
        /// <summary>Default execution order for relics that do not care about ordering.</summary>
        public const int DefaultPriority = 100;

        public abstract string ItemId { get; }
        public abstract string DisplayName { get; }

        public virtual int Priority => DefaultPriority;
        public virtual bool IsActive => true;

        public virtual void ModifyFaces(List<int> faces) { }

        public virtual void ModifyRoll(DiceRoll roll, IRandomProvider random) { }
    }
}
