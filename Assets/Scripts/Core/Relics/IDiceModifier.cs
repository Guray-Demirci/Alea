using System.Collections.Generic;
using Tavla.Core.Runtime;

namespace Tavla.Core.Relics
{
    /// <summary>
    /// Relics that interfere with the dice system.
    /// There are TWO hooks because the four designed mechanics touch two different moments:
    ///   ModifyFaces -> BEFORE the roll, changes the faces of the die (Storm Report)
    ///   ModifyRoll  -> AFTER the roll, interprets the result (Meridian Shift, Twin Winds, Cartographer's Cheat)
    /// </summary>
    public interface IDiceModifier
    {
        /// <summary>Lower values run first. Face modifiers must run before result interpreters.</summary>
        int Priority { get; }

        /// <summary>Is the relic still active? (Single-use relics return false once spent.)</summary>
        bool IsActive { get; }

        /// <summary>Modifies the pool of face values. The list is edited in place.</summary>
        void ModifyFaces(List<int> faces);

        /// <summary>Modifies the rolled result.</summary>
        void ModifyRoll(DiceRoll roll, IRandomProvider random);
    }
}
