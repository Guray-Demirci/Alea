using System;
using Tavla.Core.Abilities;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// Tahtadaki her pulun ortak iskeleti. SAF C# — Unity'den haberi yoktur.
    /// Bilincli olarak "ince" tutuldu: 18 ozel pulun hicbir yetenegi buraya EKLENMEZ.
    /// Yetenekler ICheckerAbility arayuzleriyle kompozisyon olarak takilir.
    ///
    /// Kalitim derinligi her zaman 2'dir: BaseChecker -> SomutPul. Zincir hic uzamaz;
    /// cesitlilik "hangi arayuzleri uyguladigindan" gelir.
    /// </summary>
    public abstract class BaseChecker
    {
        /// <summary>Pulun sahibi (0 veya 1).</summary>
        public int OwnerId { get; private set; }

        /// <summary>Pul tipinin kimligi. View bu degere bakarak dogru prefab'i sahneye basar.</summary>
        public abstract string TypeId { get; }

        /// <summary>
        /// Sisli Hane bu bayragi acar. Acikken GetAbility her zaman null doner,
        /// yani pul gecici olarak DefaultChecker gibi davranir.
        /// </summary>
        public bool AbilitiesSuppressed { get; private set; }

        /// <summary>Pul kirildiginda tetiklenir (View animasyonu icin).</summary>
        public event Action<BaseChecker> Captured;

        protected BaseChecker(int ownerId)
        {
            OwnerId = ownerId;
        }

        /// <summary>
        /// YETENEK KAPISI — mimarinin kalbi.
        /// GameState hicbir yerde "if (checker is ZirhliPul)" yazmaz; bunun yerine
        /// "bu pulda X yetenegi var mi?" diye sorar. Sis efekti de tek satirda burada uygulanir.
        /// </summary>
        /// <returns>Yetenek varsa kendisi, yoksa veya bastirilmissa null.</returns>
        public T GetAbility<T>() where T : class, ICheckerAbility
        {
            if (AbilitiesSuppressed)
                return null;

            return this as T;
        }

        /// <summary>Sisli Hane giris/cikisinda GameState tarafindan cagrilir.</summary>
        public void SetAbilitiesSuppressed(bool suppressed)
        {
            AbilitiesSuppressed = suppressed;
        }

        /// <summary>
        /// Bu pul kirilabilir mi? Artik override edilmez — cevap yeteneklerden gelir.
        /// Boylece Sis icindeyken Zirhli Pul da kirilabilir hale gelir (dokumandaki kural).
        /// </summary>
        public bool CanBeCaptured
        {
            get
            {
                ICaptureImmunity immunity = GetAbility<ICaptureImmunity>();
                return immunity == null || !immunity.IsImmune;
            }
        }

        /// <summary>Pul kirildigi ANDA GameState tarafindan cagrilir.</summary>
        public virtual void OnCaptured(int capturedByPlayerId)
        {
            Captured?.Invoke(this);
        }

        /// <summary>Yardimci: Verilen oyuncuya gore bu pul dusman mi?</summary>
        public bool IsEnemyOf(int playerId) => OwnerId != playerId;
    }
}
