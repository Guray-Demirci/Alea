using Tavla.Core.Abilities;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// Common skeleton of every checker on the board. PURE C# — it knows nothing about Unity.
    /// Intentionally kept "thin": none of the special abilities is ADDED here.
    /// Abilities are attached through composition via <see cref="ICheckerAbility"/> interfaces.
    ///
    /// The chain stays short: BaseChecker -> DefaultChecker, or BaseChecker -> <see cref="SpecialChecker"/> -> a
    /// concrete special checker. SpecialChecker only carries the shop identity; variety comes from "which
    /// interfaces it implements".
    /// </summary>
    public abstract class BaseChecker
    {
        /// <summary>Owner of the checker (0 or 1).</summary>
        public int OwnerId { get; private set; }

        /// <summary>Identifier of the checker type. The View uses it to spawn the right visual.</summary>
        public abstract string TypeId { get; }

        /// <summary>
        /// Set by the Fog Tile. While set, GetAbility always returns null,
        /// i.e. the checker temporarily behaves like a DefaultChecker.
        /// </summary>
        public bool AbilitiesSuppressed { get; private set; }

        /// <summary>
        /// Set by GameState while a captured checker waits to come back: from the moment it is sent to the bar until
        /// its entry move is over. Like the Fog, it makes the checker a plain one meanwhile, so its entry triggers
        /// none of its own abilities (a Bowman entering with a 6 shoots nothing).
        /// </summary>
        public bool IsReentering { get; private set; }

        protected BaseChecker(int ownerId)
        {
            OwnerId = ownerId;
        }

        /// <summary>
        /// ABILITY GATE — the heart of the architecture.
        /// GameState never writes "if (checker is ArmoredChecker)"; instead it asks
        /// "does this checker have ability X?". The Fog effect and the bar's are applied here in a single line.
        /// </summary>
        /// <returns>The ability if present, otherwise (or if suppressed) null.</returns>
        public T GetAbility<T>() where T : class, ICheckerAbility
        {
            if (AbilitiesSuppressed || IsReentering)
                return null;

            return this as T;
        }

        /// <summary>Called by the Fog Tile when a checker enters or leaves it.</summary>
        internal void SetAbilitiesSuppressed(bool suppressed)
        {
            AbilitiesSuppressed = suppressed;
        }

        /// <summary>Called by GameState when the checker is sent to the bar and once it has entered again.</summary>
        internal void SetReentering(bool reentering)
        {
            IsReentering = reentering;
        }

        /// <summary>
        /// Can this checker be captured? No longer overridden — the answer comes from abilities.
        /// That is how an Armored Checker becomes capturable inside the Fog (as designed).
        /// </summary>
        public bool CanBeCaptured
        {
            get
            {
                ICaptureImmunity immunity = GetAbility<ICaptureImmunity>();
                return immunity == null || !immunity.IsImmune;
            }
        }

        /// <summary>
        /// Do special tiles act on this checker? Not on one immune to them (Reckless). GameState asks this before
        /// every per-checker tile hook, so tiles never see such a checker at all.
        /// </summary>
        public bool IsAffectedByTiles => GetAbility<ITileImmunity>() == null;

        /// <summary>Helper: is this checker an enemy of the given player?</summary>
        public bool IsEnemyOf(int playerId) => OwnerId != playerId;
    }
}
