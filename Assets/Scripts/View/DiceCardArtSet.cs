using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tavla.View
{
    /// <summary>
    /// How the dice cards look and turn over, as an asset (Create > Tavla > Dice Card Art), cut from the artist's
    /// kartlaarkayüz (the back), zarkartlarıön_hazır (the faces) and the flips 1kartanimasyon to 6kartanimasyon.
    ///
    /// Every sprite is the upright 17x26 card, pivoted on its centre, which is where
    /// <see cref="BoardLayout.GetDiceCardPosition"/> puts it. The narrow frames of a flip keep the full canvas, so
    /// swapping frames never moves the card.
    /// </summary>
    [CreateAssetMenu(fileName = "DiceCardArt", menuName = "Tavla/Dice Card Art")]
    public sealed class DiceCardArtSet : ScriptableObject
    {
        [Serializable]
        private sealed class FaceArt
        {
            [Tooltip("The card turning over from its back to this face, frame by frame as in Nkartanimasyon: the " +
                     "first frame is the back at rest, the last the face at rest. Played backwards to turn it down.")]
            public Sprite[] Flip = new Sprite[0];
        }

        [Tooltip("The back of a card, shown while it lies face down (kartlaarkayüz). Every flip starts on it.")]
        [SerializeField] private Sprite _back;

        [Tooltip("The faces, from 1 to 6.")]
        [SerializeField] private FaceArt[] _faces = new FaceArt[DiceCardFaces.CardCount];

        [Tooltip("How long each frame of a flip is shown; a turn shows the frames after the first, so with 6 frames it " +
                 "lasts 4 x this. The artist's files use 100 ms; the game plays them faster.")]
        [SerializeField, Min(0.01f)] private float _frameSeconds = 0.07f;

        public float FrameSeconds => _frameSeconds;

        /// <summary>Does the back and every face have a complete flip?</summary>
        public bool IsComplete
        {
            get
            {
                if (_back == null || _faces == null || _faces.Length != DiceCardFaces.CardCount)
                    return false;

                foreach (FaceArt face in _faces)
                {
                    if (face == null || face.Flip == null || face.Flip.Length < 2)
                        return false;

                    foreach (Sprite frame in face.Flip)
                    {
                        if (frame == null)
                            return false;
                    }
                }

                return true;
            }
        }

        /// <summary>
        /// The card at rest: a face (1-6), or the back for <see cref="DiceCardFaces.FaceDown"/>; null if it has no art.
        /// </summary>
        public Sprite GetSprite(int face)
        {
            if (face == DiceCardFaces.FaceDown)
                return _back;

            IReadOnlyList<Sprite> flip = GetFlip(face);
            return flip != null && flip.Count > 0 ? flip[flip.Count - 1] : null;
        }

        /// <summary>The flip from the back to a face (1-6), first frame to last; null if the face has none.</summary>
        public IReadOnlyList<Sprite> GetFlip(int face)
        {
            int index = face - 1;
            if (_faces == null || index < 0 || index >= _faces.Length || _faces[index] == null)
                return null;

            return _faces[index].Flip;
        }

        private void OnValidate()
        {
            // Exactly one entry per face, so the list index always matches the face.
            if (_faces == null || _faces.Length != DiceCardFaces.CardCount)
                Array.Resize(ref _faces, DiceCardFaces.CardCount);
        }
    }
}
