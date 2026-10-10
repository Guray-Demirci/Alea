namespace Tavla.View
{
    /// <summary>
    /// The single-use cards a <see cref="RunConfig"/> can put in the shop, as an Inspector dropdown.
    /// Assets store the NUMBER, so the values are explicit: never renumber an entry, only append new ones.
    /// </summary>
    public enum CardKind
    {
        MeridianShift = 0,
        CartographersCheat = 1
    }
}
