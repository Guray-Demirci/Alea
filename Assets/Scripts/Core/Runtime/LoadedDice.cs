using System;
using System.Collections.Generic;
using Tavla.Core.Board;
using Tavla.Core.Relics;

namespace Tavla.Core.Runtime
{
    /// <summary>
    /// Dice that land on chosen faces — a testing tool, never sold in the shop. Registered on a player's dice
    /// (<see cref="MatchContext.RegisterDiceModifier"/>), it settles every roll of that player on
    /// <see cref="First"/>-<see cref="Second"/> while it is loaded, and does nothing once unloaded.
    ///
    /// It runs after every relic, so it has the final word on the pair. What comes after the relics still
    /// happens — the Anchor's pinned die, Poisoned Rum's lost die, the Kraken waking on a double — and the
    /// single extra dice that abilities roll mid-turn (Dice Holder, Second Chance) stay random, so the abilities
    /// under test behave as they do in a real match. The sandbox's god-mode panel loads it.
    /// </summary>
    public sealed class LoadedDice : IDiceModifier
    {
        /// <summary>The face the first die lands on; 0 while unloaded.</summary>
        public int First { get; private set; }

        /// <summary>The face the second die lands on; 0 while unloaded.</summary>
        public int Second { get; private set; }

        public bool IsLoaded => First > 0;

        /// <summary>After every relic: the loaded pair overrides whatever they made of the roll.</summary>
        public int Priority => int.MaxValue;

        public bool IsActive => IsLoaded;

        /// <summary>Every following roll lands on these faces, until <see cref="Unload"/>.</summary>
        /// <exception cref="ArgumentOutOfRangeException">A face outside 1..6.</exception>
        public void Load(int first, int second)
        {
            ThrowIfNotAFace(first, nameof(first));
            ThrowIfNotAFace(second, nameof(second));

            First = first;
            Second = second;
        }

        /// <summary>The dice roll freely again.</summary>
        public void Unload()
        {
            First = 0;
            Second = 0;
        }

        public void ModifyFaces(List<int> faces)
        {
        }

        public void ModifyRoll(DiceRoll roll, IRandomProvider random)
        {
            if (IsLoaded)
                roll.Set(First, Second);
        }

        private static void ThrowIfNotAFace(int value, string paramName)
        {
            if (value < 1 || value > GameState.MaxDieValue)
                throw new ArgumentOutOfRangeException(paramName, value,
                    $"A die face must be in the range 1..{GameState.MaxDieValue}.");
        }
    }
}
