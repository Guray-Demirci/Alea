using Tavla.Core.Board;
using Tavla.Core.Checkers;
using Tavla.Core.Runtime;

namespace Tavla.Core.Interfaces
{
    /// <summary>
    /// A tile that locks checkers in place and lets their owner free one by sacrificing a die (Swamp).
    /// Not every lock is releasable: the Dungeon's bars only open with the round, so it does not implement this.
    ///
    /// The tile only decides WHETHER a checker can be freed and forgets the lock; spending the die belongs to
    /// GameState's dice bridge (<see cref="GameState.TryReleaseChecker"/>), like every other use of the dice.
    /// </summary>
    public interface IReleasableLock
    {
        /// <summary>
        /// Can this checker be freed right now? Called during VALIDATION — must have no side effects.
        /// False if the checker is not locked, or if the rules do not allow freeing it yet.
        /// </summary>
        bool CanRelease(BaseChecker checker);

        /// <summary>Forgets the lock. Called only after <see cref="CanRelease"/> returned true and the die was spent.</summary>
        void Release(IMatchContext context, Node node, BaseChecker checker);
    }
}
