using Tavla.Core.Abilities;
using Tavla.Core.Interfaces;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// B - Kara Delik Pulu (Vortex). "Hanenin pul sinirini kaldirir; kendi rengindeki
    /// tum pullari tek hanede toplayip devasa bir SUPER KAPI olusturabilirsin."
    /// Kapasite kurali artik sabit degil, hanede duran pullara sorulan bir soruya donustu.
    /// </summary>
    public class VortexChecker : BaseChecker, IStackLimitOverride, IPermanent
    {
        public const string Id = "checker_vortex";

        public override string TypeId => Id;

        /// <summary>Sinir tamamen kalkar. Sisli Hane'de bu yetenek duser ve limit geri gelir.</summary>
        public int GetStackLimit(int defaultLimit) => int.MaxValue;

        public string ItemId => Id;
        public string DisplayName => "Kara Delik Pulu";
        public int Price => 850;

        /// <summary>Ayni puldan birden fazlasi desteye konabilir.</summary>
        public bool AllowsDuplicates => true;

        public VortexChecker(int ownerId) : base(ownerId) { }
    }
}
