using Tavla.Core.Board;
using Tavla.Core.Runtime;

namespace Tavla.Core.Abilities
{
    /// <summary>
    /// Checkers that react AFTER a move has completed. Validation is over at this point,
    /// so changing game state here is allowed (score, gold, dice, extra captures...).
    ///
    /// GameState invokes this hook for two groups:
    ///   isMover = true  -> the checker that made the move (Navigator, Catalyst, Anchor, Bouncer, Double Down, Time Bender)
    ///   isMover = false -> other checkers ALREADY standing on the target point (Siege, Gemini)
    /// This way checkers that trigger "when a friendly checker arrives on my point" need no separate interface.
    /// </summary>
    public interface IMoveReactor : ICheckerAbility
    {
        void OnMoveCompleted(IMatchContext context, MoveResult result, bool isMover);
    }
}
