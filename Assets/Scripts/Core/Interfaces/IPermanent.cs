namespace Tavla.Core.Interfaces
{
    /// <summary>
    /// PURCHASABLE — PERMANENT BRANCH.
    /// Bought once and kept for good: special checkers (<see cref="Checkers.SpecialChecker"/>) and permanent dice
    /// relics (<see cref="Relics.PermanentRelic"/>). The player owns at most one of each — a deck holds one of each
    /// special checker (<see cref="Run.RunState.AddToDeck"/>) and a second charm would do nothing — so the shop never
    /// offers an owned one.
    ///
    /// Mutually exclusive with <see cref="IConsumable"/>. An item must implement exactly ONE of them;
    /// an inventory system that sees a type implementing only <see cref="IPurchasable"/> may treat it as an error.
    /// </summary>
    public interface IPermanent : IPurchasable
    {
    }
}
