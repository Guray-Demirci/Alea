using Tavla.Core.Abilities;
using Tavla.Core.Board;
using Tavla.Core.Interfaces;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// B - Parazit Pul. "Rakibin kapi aldigi (en az 2 pulunun oldugu) bir haneye sinsice sizabilir."
    /// Sizma sirasinda KIRMA yapilmaz; rakiple ayni hanede birlikte durur.
    /// Node sinifinin cok sahipli hale getirilmesinin sebebi tam olarak bu puldur.
    /// </summary>
    public class ParasiteChecker : BaseChecker, IInfiltrator, IPermanent
    {
        public const string Id = "checker_parasite";

        public override string TypeId => Id;

        /// <summary>Sadece gercek bir KAPIYA sizar (2+ rakip pulu). Tek acik pulu normal kurallarla kirar.</summary>
        public bool CanInfiltrate(Node target) => target.CountEnemiesOf(OwnerId) >= 2;

        public string ItemId => Id;
        public string DisplayName => "Parazit Pul";
        public int Price => 420;

        /// <summary>Ayni puldan birden fazlasi desteye konabilir.</summary>
        public bool AllowsDuplicates => true;

        public ParasiteChecker(int ownerId) : base(ownerId) { }
    }
}
