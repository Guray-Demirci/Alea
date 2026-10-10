using Tavla.Core.Runtime;

namespace Tavla.Core.Interfaces
{
    /// <summary>
    /// PURCHASABLE — SINGLE-USE BRANCH.
    /// Items with a limited number of uses that are gone once spent.
    /// Meridian Shift and Cartographer's Cheat live here; future single-use
    /// cards/traps belong to this branch as well.
    ///
    /// The charge count is intentionally not fixed at 1: adding a "3 uses" card
    /// requires only a different MaxCharges value, not a new interface.
    /// </summary>
    public interface IConsumable : IPurchasable
    {
        /// <summary>Number of uses when fully charged.</summary>
        int MaxCharges { get; }

        /// <summary>Remaining uses.</summary>
        int RemainingCharges { get; }

        /// <summary>Are all charges spent? Depleted items are removed from the inventory.</summary>
        bool IsDepleted { get; }

        /// <summary>
        /// Does the player need to pick a value before using it?
        /// (Meridian Shift: "which number should the die be pinned to?") The UI opens a picker based on this.
        /// </summary>
        bool RequiresChoice { get; }

        /// <summary>
        /// Arms the item by spending one charge. Returns false and changes nothing if no charges are left.
        /// </summary>
        /// <param name="choice">Ignored when <see cref="RequiresChoice"/> is false.</param>
        bool TryActivate(IMatchContext context, int choice);
    }
}
