using System.Collections.Generic;
using Tavla.Core.Relics;

namespace Tavla.Core.Runtime
{
    /// <summary>
    /// Zar atma isleminin tek sorumlusu. Tilsimlari TANIMAZ, sadece IDiceModifier listesini
    /// oncelik sirasina gore calistirir — yeni bir zar tilsimi eklemek bu sinifi degistirmez.
    /// </summary>
    public class DiceRoller
    {
        /// <summary>Standart zar yuzeyleri. Firtina Raporu bu havuzu degistirebilir.</summary>
        private static readonly int[] StandardFaces = { 1, 2, 3, 4, 5, 6 };

        private readonly IRandomProvider _random;
        private readonly List<IDiceModifier> _sorted = new List<IDiceModifier>();

        public DiceRoller(IRandomProvider random)
        {
            _random = random;
        }

        public DiceRoll Roll(IList<IDiceModifier> modifiers)
        {
            // Etkin tilsimlari oncelige gore sirala (her atista yeniden: tilsim tukenmis olabilir).
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

            // 1) Atistan ONCE: zar yuzeylerini kurgula.
            List<int> faces = new List<int>(StandardFaces);
            for (int i = 0; i < _sorted.Count; i++)
                _sorted[i].ModifyFaces(faces);

            if (faces.Count == 0)
                faces.AddRange(StandardFaces); // Guvenlik: tum yuzler silinirse standarda don.

            // 2) Atis.
            DiceRoll roll = new DiceRoll();
            roll.Set(faces[_random.Range(0, faces.Count)], faces[_random.Range(0, faces.Count)]);

            // 3) Atistan SONRA: sonucu yorumla.
            for (int i = 0; i < _sorted.Count; i++)
                _sorted[i].ModifyRoll(roll, _random);

            return roll;
        }
    }
}
