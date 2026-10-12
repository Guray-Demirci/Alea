using System;
using System.Collections.Generic;
using Tavla.Core.Board;
using Tavla.Core.Runtime;

namespace Tavla.View
{
    /// <summary>
    /// Which face each dice card in the middle of the board shows. There is one card per die face, 1 to 6 from
    /// left to right, and they lie face down until the dice say otherwise:
    ///   a die in hand turns up its own card (5-2: the 2 and the 5),
    ///   a double turns up the middle four cards instead (cards 2-5), all on the doubled face, one per move left,
    ///   a spent die's card turns back down.
    ///
    /// Kept apart from <see cref="DiceCardsView"/> so the rule can be tested without a scene. Core's dice are read
    /// as they are; no rule of the game is decided here.
    /// </summary>
    public static class DiceCardFaces
    {
        /// <summary>One card per die face.</summary>
        public const int CardCount = GameState.MaxDieValue;

        /// <summary>A card lying face down.</summary>
        public const int FaceDown = 0;

        // A double's moves are shown on the middle four cards, filled from the left.
        private const int FirstDoubleCard = 1;
        private const int DoubleCardCount = 4;

        /// <summary>Fills <paramref name="faces"/> (one entry per card) with what the dice in hand show.</summary>
        /// <param name="roll">The current dice; null before the first roll.</param>
        /// <exception cref="ArgumentException"><paramref name="faces"/> does not have one entry per card.</exception>
        public static void Compute(DiceRoll roll, int[] faces)
        {
            if (roll == null)
            {
                Compute(false, null, faces);
                return;
            }

            if (!roll.FreeSplit)
            {
                Compute(roll.IsDouble, roll.RemainingMoves, faces);
                return;
            }

            // Cartographer's Cheat: the dice became one pool to split freely, so the rolled dice stay up until the
            // pool is spent. A double's pool is worth only its two dice, so it shows two cards.
            ThrowIfWrongLength(faces);
            Array.Clear(faces, 0, faces.Length);

            if (roll.FreeTotal <= 0)
                return;

            if (roll.IsDouble)
            {
                ShowDouble(roll.First, 2, faces);
            }
            else
            {
                TurnUp(roll.First, faces);
                TurnUp(roll.Second, faces);
            }
        }

        /// <summary>The same rule on plain values, for a roll spent die by die.</summary>
        /// <param name="rolledDouble">Did the dice land on a double?</param>
        /// <param name="moves">The die values still in hand; null or empty when none is left.</param>
        /// <exception cref="ArgumentException"><paramref name="faces"/> does not have one entry per card.</exception>
        public static void Compute(bool rolledDouble, IReadOnlyList<int> moves, int[] faces)
        {
            ThrowIfWrongLength(faces);
            Array.Clear(faces, 0, faces.Length);

            if (moves == null || moves.Count == 0)
                return;

            // A double rerolled mid-turn (Dice Holder) may no longer be one value; then each die shows its own card.
            if (rolledDouble && AllSame(moves))
            {
                ShowDouble(moves[0], moves.Count, faces);
                return;
            }

            for (int i = 0; i < moves.Count; i++)
                TurnUp(moves[i], faces);
        }

        private static void ShowDouble(int face, int moveCount, int[] faces)
        {
            if (!IsFace(face))
                return;

            // Extra moves granted on a double (Double Down) do not fit; the HUD still counts them.
            int count = Math.Min(moveCount, DoubleCardCount);
            for (int i = 0; i < count; i++)
                faces[FirstDoubleCard + i] = face;
        }

        private static void TurnUp(int face, int[] faces)
        {
            if (IsFace(face))
                faces[face - 1] = face;
        }

        private static bool IsFace(int value) => value >= 1 && value <= CardCount;

        private static bool AllSame(IReadOnlyList<int> moves)
        {
            for (int i = 1; i < moves.Count; i++)
            {
                if (moves[i] != moves[0])
                    return false;
            }

            return true;
        }

        private static void ThrowIfWrongLength(int[] faces)
        {
            if (faces == null || faces.Length != CardCount)
                throw new ArgumentException($"Expected one entry per card ({CardCount}).", nameof(faces));
        }
    }
}
