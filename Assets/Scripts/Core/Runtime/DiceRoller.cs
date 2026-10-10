using System.Collections.Generic;
using Tavla.Core.Relics;

namespace Tavla.Core.Runtime
{
    /// <summary>
    /// The single owner of rolling dice. It does NOT know relics; it only runs the
    /// <see cref="IDiceModifier"/> list in priority order — adding a new dice relic does not change this class.
    /// </summary>
    public class DiceRoller
    {
        /// <summary>Standard die faces. Storm Report may change this pool.</summary>
        private static readonly int[] StandardFaces = { 1, 2, 3, 4, 5, 6 };

        private readonly IRandomProvider _random;
        private readonly List<IDiceModifier> _sorted = new List<IDiceModifier>();
        private readonly List<int> _faces = new List<int>(StandardFaces.Length);

        public DiceRoller(IRandomProvider random)
        {
            _random = random;
        }

        public DiceRoll Roll(IList<IDiceModifier> modifiers)
        {
            // 1) BEFORE the roll: shape the die faces.
            PrepareFaces(modifiers);

            // 2) The roll.
            DiceRoll roll = new DiceRoll();
            roll.Set(RollPreparedFace(), RollPreparedFace());

            // 3) AFTER the roll: interpret the result.
            for (int i = 0; i < _sorted.Count; i++)
                _sorted[i].ModifyRoll(roll, _random);

            return roll;
        }

        /// <summary>
        /// One extra die, for abilities that roll again mid-turn (Dice Holder, Second Chance). The relics still
        /// shape its faces, but nothing interprets a single die the way a pair is interpreted.
        /// </summary>
        public int RollFace(IList<IDiceModifier> modifiers)
        {
            PrepareFaces(modifiers);
            return RollPreparedFace();
        }

        private void PrepareFaces(IList<IDiceModifier> modifiers)
        {
            // Sort active relics by priority (on every roll: a relic may have been used up).
            _sorted.Clear();
            if (modifiers != null)
            {
                for (int i = 0; i < modifiers.Count; i++)
                {
                    if (modifiers[i] != null && modifiers[i].IsActive)
                        _sorted.Add(modifiers[i]);
                }
                _sorted.Sort((a, b) => a.Priority.CompareTo(b.Priority));
            }

            _faces.Clear();
            _faces.AddRange(StandardFaces);
            for (int i = 0; i < _sorted.Count; i++)
                _sorted[i].ModifyFaces(_faces);

            if (_faces.Count == 0)
                _faces.AddRange(StandardFaces); // Safety: fall back to standard if every face was removed.
        }

        private int RollPreparedFace() => _faces[_random.Range(0, _faces.Count)];
    }
}
