using Tavla.Core.Board;

namespace Tavla.Core.Runtime
{
    /// <summary>
    /// The SINGLE gateway through which abilities can touch the game.
    /// The designed checkers touch very different systems: score, gold, dice and turn order.
    /// Instead of injecting a separate service into every ability we provide one contract:
    ///  - ability signatures stay uniform (OnMoveCompleted(IMatchContext, ...)),
    ///  - writing a fake MatchContext is enough for tests,
    ///  - when a new system arrives only this interface gains a method; 30 classes stay untouched.
    /// </summary>
    public interface IMatchContext
    {
        GameState Board { get; }
        IRandomProvider Random { get; }
        DiceRoll CurrentRoll { get; }

        // --- Score ---
        /// <summary>For checkers that produce flat points, such as the Siege.</summary>
        /// <returns>The score actually written (0 when a simulation discards it).</returns>
        int AddScore(int playerId, int amount);

        /// <summary>The player's total score. GameState uses it to decide the winner at the end of the match.</summary>
        int GetScore(int playerId);

        // --- Economy ---
        void AddGold(int playerId, int amount);

        /// <summary>Sacrificial Checker: permanently discounts every item in the shop.</summary>
        void AddShopDiscount(int amount);

        /// <summary>Phoenix Checker: the next shop offers only rare+ items.</summary>
        void TriggerLuxuryShop(int playerId);

        // --- Turn / dice ---
        /// <summary>Time Bender Checker: makes the target player skip their turn.</summary>
        void SkipTurn(int playerId);

        /// <summary>Anchor Checker &amp; Meridian Shift: pins one die of the next roll.</summary>
        void PinNextDie(int playerId, int value);

        /// <summary>Poisoned Rum Checker: the player's next roll has a single die; the second is passed.</summary>
        void RestrictNextRollToOneDie(int playerId);

        /// <summary>Dice Holder Checker: rolls the player's unused dice of this turn again.</summary>
        void RerollRemainingDice(int playerId);

        /// <summary>Second Chance Checker: rolls one more die and adds it to the player's moves this turn.</summary>
        void GrantExtraDie(int playerId);
    }
}
