using Tavla.Core.Abilities;
using Tavla.Core.Interfaces;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// B - Zirhli Pul. "Asla kirilamaz. Tek basina dursa bile kapi sayilir."
    /// IKI ayri yetenek uyguluyor cunku bunlar iki farkli kuraldir:
    /// kirilmazlik (ICaptureImmunity) ile tek basina kapi olmak (IBlockadeOverride) bagimsizdir.
    /// Sisli Hane'ye girerse GetAbility null doner ve ikisi de kendiliginden duser.
    /// </summary>
    public class ArmoredChecker : BaseChecker, ICaptureImmunity, IBlockadeOverride, IPermanent
    {
        public const string Id = "checker_armored";

        public override string TypeId => Id;

        // --- ICaptureImmunity ---
        public bool IsImmune => true;                 // Kalici zirh, hak tukenmez.
        public void OnImmunityTriggered() { }         // Harcanacak bir sey yok.

        // --- IBlockadeOverride ---
        public bool FormsBlockadeAlone => true;

        // --- IPurchasable ---
        public string ItemId => Id;
        public string DisplayName => "Zırhlı Pul";
        public int Price => 500;

        /// <summary>Ayni puldan birden fazlasi desteye konabilir.</summary>
        public bool AllowsDuplicates => true;

        public ArmoredChecker(int ownerId) : base(ownerId) { }
    }
}
