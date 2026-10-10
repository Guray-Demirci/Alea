using Tavla.Core.Checkers;

namespace Tavla.Core.Run
{
    /// <summary>
    /// One checker of the player's deck: what it is, and how to make a fresh one for every match.
    /// Created by <see cref="RunState.AddToDeck"/>.
    /// </summary>
    public sealed class DeckEntry
    {
        private readonly CheckerFactory _create;

        /// <summary>The checker's <see cref="BaseChecker.TypeId"/>.</summary>
        public string TypeId { get; }

        /// <summary>Anything but a default checker. The shop limits how many of these a deck may hold.</summary>
        public bool IsSpecial => TypeId != DefaultChecker.Id;

        internal DeckEntry(string typeId, CheckerFactory create)
        {
            TypeId = typeId;
            _create = create;
        }

        internal BaseChecker Create(int ownerId) => _create(ownerId);
    }
}
