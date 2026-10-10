using Tavla.Core.Runtime;

namespace Tavla.Core.Abilities
{
    /// <summary>
    /// Fired when the checker is CAPTURED. Defines the SIDE EFFECT, not the destination:
    /// the Sacrificial Checker discounts the shop, the Phoenix Checker opens the luxury shop, the Poisoned Rum
    /// Checker takes a die from the capturer's next roll.
    /// (The destination is a separate responsibility -> <see cref="ICustomCaptureDestination"/>.)
    /// </summary>
    public interface ICaptureReactor : ICheckerAbility
    {
        void OnCaptured(IMatchContext context, int capturedByPlayerId, int nodeIndex);
    }
}
