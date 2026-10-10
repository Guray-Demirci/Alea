using Tavla.Core.Interfaces;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// Common skeleton of the special checkers: the identity the shop sells them by, written once.
    /// It adds NO behaviour — abilities still come only from the <see cref="Abilities.ICheckerAbility"/> interfaces
    /// a concrete checker implements, so this layer never grows rules of its own.
    ///
    /// The type id doubles as the shop's item id: a deck holds one of each special checker
    /// (<see cref="Run.RunState.AddToDeck"/>), so the two never need to differ.
    /// </summary>
    public abstract class SpecialChecker : BaseChecker, IPermanent
    {
        private readonly string _typeId;

        public override string TypeId => _typeId;

        public string ItemId => _typeId;

        public string DisplayName { get; }

        /// <param name="typeId">The concrete checker's <c>Id</c> constant.</param>
        /// <param name="displayName">The name the shop shows.</param>
        protected SpecialChecker(int ownerId, string typeId, string displayName) : base(ownerId)
        {
            _typeId = typeId;
            DisplayName = displayName;
        }
    }
}
