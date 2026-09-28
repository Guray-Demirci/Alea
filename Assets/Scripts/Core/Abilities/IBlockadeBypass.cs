using Tavla.Core.Board;

namespace Tavla.Core.Abilities
{
    /// <summary>
    /// Ghost Checker: "Can jump over the opponent's points and ignores blockades."
    /// Queried during move VALIDATION, so implementations must be free of side effects.
    /// </summary>
    public interface IBlockadeBypass : ICheckerAbility
    {
        /// <summary>Can the blockade on the target point be ignored?</summary>
        bool CanBypassBlockade(Node target);
    }
}
