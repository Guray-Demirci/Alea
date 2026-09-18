using Tavla.Core.Interfaces;

namespace Tavla.Core.Relics
{
    /// <summary>
    /// KALICI tilsimlarin ortak iskeleti. Bir kez satin alinir, mac boyunca her atista calisir.
    /// Hak sayaci yoktur; bu yuzden ConsumableRelic ile hicbir kod paylasmaz — iki dal
    /// bilerek ayri tutuldu.
    /// </summary>
    public abstract class PermanentRelic : BaseRelic, IPermanent
    {
        /// <summary>
        /// Ayni tilsimdan ikincisi hicbir sey yapmaz (etki ust uste binmez),
        /// bu yuzden dukkan tek satistan sonra kilitler.
        /// </summary>
        public virtual bool AllowsDuplicates => false;

        /// <summary>Kalici tilsim her zaman etkindir.</summary>
        public override bool IsActive => true;
    }
}
