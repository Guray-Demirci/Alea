using System;
using UnityEngine;

namespace Tavla.View
{
    /// <summary>
    /// How one side's checkers are drawn in each state. Purely visual — whether a checker CAN move is asked of
    /// Core; the skin only says what that looks like.
    ///
    /// A skin with art is drawn in its own colours. Without art the checker keeps the sprite it was built with
    /// (the white checker sprite or a prefab's art) and takes the player colour.
    /// </summary>
    [Serializable]
    public sealed class CheckerSkin
    {
        [Tooltip("The checker at rest. Empty = the white checker sprite, tinted with the player colour.")]
        public Sprite Normal;

        [Tooltip("Shown while the pointer is over the checker and it can be moved with the rolled dice. " +
                 "Empty = the Normal look.")]
        public Sprite Movable;

        [Tooltip("Shown while the checker is picked up. Empty = the Normal look.")]
        public Sprite Selected;

        [Tooltip("Drawn around the checker while the pointer is over it and it can move — for art without a Movable " +
                 "sprite of its own. Empty = none.")]
        public Sprite MovableRing;

        [Tooltip("Drawn around the checker while it is picked up — for art without a Selected sprite of its own. Empty = none.")]
        public Sprite SelectedRing;

        [Tooltip("The checker breaking apart when it is hit, frame by frame, without the frame that shows it whole. " +
                 "Played backwards where it comes back (the bar). Empty = a hit on it only shakes the screen.")]
        public Sprite[] Break = new Sprite[0];

        /// <summary>True if the skin brings its own art, drawn in its own colours.</summary>
        public bool HasArt => Normal != null;
    }
}
