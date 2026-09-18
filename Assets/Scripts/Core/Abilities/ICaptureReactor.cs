using Tavla.Core.Runtime;

namespace Tavla.Core.Abilities
{
    /// <summary>
    /// Pul KIRILDIGINDA tetiklenir. Nereye gidecegini degil, YAN ETKISINI belirler:
    /// Fedakar Pul dukkani ucuzlatir, Anka Pulu luks vitrini acar.
    /// (Gidecegi yer ayri bir sorumluluktur -> ICustomCaptureDestination.)
    /// </summary>
    public interface ICaptureReactor : ICheckerAbility
    {
        void OnCaptured(IMatchContext context, int capturedByPlayerId, int nodeIndex);
    }
}
