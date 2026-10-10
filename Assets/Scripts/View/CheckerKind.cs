namespace Tavla.View
{
    /// <summary>
    /// The special checkers a <see cref="RunConfig"/> can put in the shop, as an Inspector dropdown.
    /// Assets store the NUMBER, so the values are explicit: never renumber an entry, only append new ones.
    /// Retired numbers are never reused: 1 (Ghost), 7 (Gemini), 17 (Vortex).
    /// </summary>
    public enum CheckerKind
    {
        Armored = 0,
        Nomad = 2,
        Parasite = 3,
        TimeBender = 4,
        Clover = 5,
        DoubleDown = 6,
        Catalyst = 8,
        Navigator = 9,
        Siege = 10,
        Sacrificial = 11,
        Golden = 12,
        Bandit = 13,
        Anchor = 14,
        Phoenix = 15,
        Doomsday = 16,
        Drunken = 18,
        Gunpowder = 19,
        Voodoo = 20,
        Reckless = 21,
        Teleporter = 22,
        SecondChance = 23,
        Grappling = 24,
        PoisonedRum = 25,
        DiceHolder = 26,
        Kraken = 27,
        Mutiny = 28,
        Hammer = 29
    }
}
