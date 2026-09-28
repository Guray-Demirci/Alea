using System;
using Tavla.Core.Abilities;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// Common skeleton of every checker on the board. PURE C# — it knows nothing about Unity.
    /// Intentionally kept "thin": none of the 18 special abilities is ADDED here.
    /// Abilities are attached through composition via <see cref="ICheckerAbility"/> interfaces.
    ///
    /// The inheritance depth is always 2: BaseChecker -> ConcreteChecker. The chain never grows;
    /// variety comes from "which interfaces it implements".
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

        /// <summary>Raised when the checker is captured (for View animations).</summary>
        public event Action<BaseChecker> Captured;

        protected BaseChecker(int ownerId)
        {
            OwnerId = ownerId;
        }

        /// <summary>
        /// ABILITY GATE — the heart of the architecture.
        /// GameState never writes "if (checker is ArmoredChecker)"; instead it asks
        /// "does this checker have ability X?". The Fog effect is applied here in a single line.
        /// </summary>
        /// <returns>The ability if present, otherwise (or if suppressed) null.</returns>
        public T GetAbility<T>() where T : class, ICheckerAbility
        {
            if (AbilitiesSuppressed)
                return null;

            return this as T;
        }

        /// <summary>Called by the Fog Tile when a checker enters or leaves it.</summary>
        internal void SetAbilitiesSuppressed(bool suppressed)
        {
            AbilitiesSuppressed = suppressed;
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

        /// <summary>Called by GameState at the MOMENT the checker is captured.</summary>
        public virtual void OnCaptured(int capturedByPlayerId)
        {
            Captured?.Invoke(this);
        }

        /// <summary>Helper: is this checker an enemy of the given player?</summary>
        public bool IsEnemyOf(int playerId) => OwnerId != playerId;
    }
}
