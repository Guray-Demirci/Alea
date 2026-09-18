namespace Tavla.Core.Abilities
{
    /// <summary>Kirilmaya karsi bagisiklik. Zirhli Pul (kalici) ve tek kullanimlik kalkan varyantlari kullanir.</summary>
    public interface ICaptureImmunity : ICheckerAbility
    {
        /// <summary>Su an bagisiklik aktif mi? (Saf sorgu, yan etkisi olmamali.)</summary>
        bool IsImmune { get; }

        /// <summary>Bagisiklik bir saldiriyi savusturdugunda cagrilir. Kalici zirhta bos gecilir.</summary>
        void OnImmunityTriggered();
    }
}
