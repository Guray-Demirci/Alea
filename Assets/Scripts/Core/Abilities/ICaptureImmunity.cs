namespace Tavla.Core.Abilities
{
    /// <summary>
    /// Immunity to being captured. Used by the Armored Checker (permanent) and by single-use shield variants.
    /// </summary>
    public interface ICaptureImmunity : ICheckerAbility
    {
        /// <summary>Is the immunity currently active? (Pure query — must have no side effects.)</summary>
        bool IsImmune { get; }

        /// <summary>Called when the immunity deflected an attack. A no-op for permanent armor.</summary>
        void OnImmunityTriggered();
    }
}
