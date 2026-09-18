using System.Collections.Generic;
using Tavla.Core.Interfaces;
using Tavla.Core.Runtime;

namespace Tavla.Core.Relics
{
    /// <summary>
    /// Zar tilsimlarinin ortak iskeleti. Pullardaki BaseChecker ile ayni felsefe:
    /// ince tutulur, davranis alt sinifta tanimlanir. Varsayilan hook'lar bos gecer,
    /// boylece her tilsim sadece ilgilendigi hook'u override eder (ISP).
    ///
    /// DIKKAT: Bu sinif bilerek sadece IPurchasable uygular — ne IConsumable ne IPermanent.
    /// Dal secimi bir alt katmanda yapilir (ConsumableRelic / PermanentRelic).
    /// abstract oldugu icin "dali belirsiz" bir tilsim ornegi hicbir zaman olusamaz;
    /// somut her tilsim iki daldan birine dusmek ZORUNDA.
    /// </summary>
    public abstract class BaseRelic : IPurchasable, IDiceModifier
    {
        public abstract string ItemId { get; }
        public abstract string DisplayName { get; }
        public abstract int Price { get; }

        public virtual int Priority => 100;
        public virtual bool IsActive => true;

        public virtual void ModifyFaces(List<int> faces) { }

        public virtual void ModifyRoll(DiceRoll roll, IRandomProvider random) { }
    }
}
