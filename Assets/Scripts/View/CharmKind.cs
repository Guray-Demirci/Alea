namespace Tavla.View
{
    /// <summary>
    /// The permanent dice charms a <see cref="RunConfig"/> can put in the shop, as an Inspector dropdown.
    /// Assets store the NUMBER, so the values are explicit: never renumber an entry, only append new ones.
    ///
    /// Storm Report is not listed yet: the player chooses which face it replaces, and there is no screen for
    /// that choice yet.
    /// </summary>
    public enum CharmKind
    {
        TwinWinds = 0
    }
}
