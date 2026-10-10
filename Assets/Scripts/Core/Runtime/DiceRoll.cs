using System;
using System.Collections.Generic;

namespace Tavla.Core.Runtime
{
    /// <summary>
    /// Dice state of one turn. Two consumption modes exist:
    ///   Normal mode : rolled values are spent one by one (classic backgammon).
    ///   FreeSplit   : with "Cartographer's Cheat" active, the SUM of the dice becomes a pool that
    ///                 can be split into any parts and distributed across any checkers.
    ///
    /// Readers (View, bot) only see the public queries. Mutators are <c>internal</c> so the dice can
    /// only be changed by Core rules (moves, abilities, relics) — never directly from the View.
    /// </summary>
    public class DiceRoll
    {
        private readonly List<int> _moves = new List<int>();

        public int First { get; private set; }
        public int Second { get; private set; }
        public bool IsDouble => First == Second;

        /// <summary>Cartographer's Cheat: the total may be split freely.</summary>
        public bool FreeSplit { get; internal set; }

        /// <summary>Remaining total in FreeSplit mode.</summary>
        public int FreeTotal { get; private set; }

        /// <summary>Remaining move values in normal mode.</summary>
        public IReadOnlyList<int> RemainingMoves => _moves;

        public bool HasMovesLeft => FreeSplit ? FreeTotal > 0 : _moves.Count > 0;

        /// <summary>Moves spent so far this turn. The Dice Holder Checker reacts to the turn's first move.</summary>
        public int MovesSpent { get; private set; }

        /// <summary>Sets the dice. A double grants 4 moves, as in the classic rules.</summary>
        internal void Set(int first, int second)
        {
            First = first;
            Second = second;
            MovesSpent = 0;

            _moves.Clear();
            _moves.Add(first);
            _moves.Add(second);

            if (first == second)
            {
                _moves.Add(first);
                _moves.Add(second);
            }

            FreeTotal = first + second;
        }

        /// <summary>Overrides one die value (used by Meridian Shift / Anchor Checker).</summary>
        internal void OverrideDie(bool overrideFirst, int value)
        {
            if (overrideFirst)
                Set(value, Second);
            else
                Set(First, value);
        }

        /// <summary>Is there a move available for the given distance? (Pure query, spends nothing.)</summary>
        public bool CanAfford(int distance)
        {
            if (distance <= 0)
                return false;

            return FreeSplit ? distance <= FreeTotal : _moves.Contains(distance);
        }

        /// <summary>Spends a move. Returns false and changes nothing if no move is available.</summary>
        internal bool Consume(int distance)
        {
            if (!CanAfford(distance))
                return false;

            if (FreeSplit)
                FreeTotal -= distance;
            else
                _moves.Remove(distance);

            MovesSpent++;
            return true;
        }

        /// <summary>Teleporter Checker: one move that costs every die still in hand.</summary>
        internal void ConsumeAll()
        {
            _moves.Clear();
            FreeTotal = 0;
            MovesSpent++;
        }

        /// <summary>Double Down and Second Chance Checkers: grants an extra move.</summary>
        internal void GrantExtraMove(int value)
        {
            _moves.Add(value);
            FreeTotal += value;
        }

        /// <summary>Poisoned Rum Checker: only the first die counts this turn, the second is passed.</summary>
        internal void DropSecondDie()
        {
            Second = 0;

            _moves.Clear();
            _moves.Add(First);
            FreeTotal = First;
        }

        /// <summary>Dice Holder Checker: every move still in hand is rolled again.</summary>
        /// <param name="rollFace">Rolls one die face.</param>
        internal void RerollRemaining(Func<int> rollFace)
        {
            for (int i = 0; i < _moves.Count; i++)
                _moves[i] = rollFace();
        }

        /// <summary>
        /// An independent copy of the dice state. When the AI asks "what happens if I play this move?"
        /// it must not spend the real dice — the clone board is bound to this copy.
        /// </summary>
        public DiceRoll Clone()
        {
            DiceRoll copy = new DiceRoll();

            copy.First = First;
            copy.Second = Second;
            copy.FreeSplit = FreeSplit;
            copy.FreeTotal = FreeTotal;
            copy.MovesSpent = MovesSpent;
            copy._moves.AddRange(_moves);

            return copy;
        }
    }
}
