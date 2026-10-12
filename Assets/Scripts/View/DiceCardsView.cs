using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tavla.View
{
    /// <summary>
    /// The six dice cards in the middle of the board. DUMB PUPPET like <see cref="ShopView"/>: it does not know
    /// the dice — <see cref="DiceCardsPresenter"/> tells it which face each card shows (<see cref="DiceCardFaces"/>);
    /// it only knows where the cards lie and how they turn over.
    ///
    /// A card turns over by playing the artist's flip (<see cref="DiceCardArtSet.GetFlip"/>): forwards to turn a face
    /// up, backwards to turn it down. Going from one face straight to another, it turns down first. A turn already
    /// started always finishes; the card then goes on to the latest face it was asked for.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DiceCardsView : MonoBehaviour
    {
        private const int NoFace = -1;

        private sealed class Card
        {
            public SpriteRenderer Renderer;

            /// <summary>The face the card rests on, or turns away from while a turn is under way.</summary>
            public int Shown;

            /// <summary>The face the card is turning to.</summary>
            public int Target;

            /// <summary>Asked for while turning; applied once the turn ends. <see cref="NoFace"/> = nothing asked.</summary>
            public int Pending = NoFace;

            public bool Turning;
            public float Elapsed;

            /// <summary>The turn's frames after the one it starts from; the last is <see cref="Target"/> at rest.</summary>
            public readonly List<Sprite> Frames = new List<Sprite>();
        }

        private readonly Card[] _cards = new Card[DiceCardFaces.CardCount];
        private readonly int[] _dealt = new int[DiceCardFaces.CardCount];

        private DiceCardArtSet _art;

        // A new roll waits for every card to lie face down before turning the new faces up.
        private bool _dealPending;

        /// <summary>Builds the cards in code from their art, face down.</summary>
        public static DiceCardsView Create(DiceCardArtSet art, BoardLayout layout)
        {
            if (art == null)
                throw new ArgumentNullException(nameof(art));

            if (layout == null)
                throw new ArgumentNullException(nameof(layout));

            DiceCardsView view = BoardSceneBuilder.CreateWorldRoot("DiceCards").gameObject.AddComponent<DiceCardsView>();
            view.Build(art, layout);
            return view;
        }

        private void Build(DiceCardArtSet art, BoardLayout layout)
        {
            _art = art;

            for (int i = 0; i < _cards.Length; i++)
            {
                GameObject go = new GameObject($"DiceCard_{i + 1}");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = layout.GetDiceCardPosition(i);

                SpriteRenderer spriteRenderer = go.AddComponent<SpriteRenderer>();
                spriteRenderer.sortingOrder = SortingOrders.DiceCards;

                _cards[i] = new Card { Renderer = spriteRenderer };
            }

            LayFaceDown();
        }

        /// <summary>Every card turns over to its new face (<see cref="DiceCardFaces"/>, one entry per card).</summary>
        public void Show(IReadOnlyList<int> faces)
        {
            ThrowIfWrongLength(faces);

            // A deal still waiting for the cards to go down simply deals these faces instead.
            if (_dealPending)
            {
                CopyToDealt(faces);
                return;
            }

            for (int i = 0; i < _cards.Length; i++)
                TurnTo(_cards[i], faces[i]);
        }

        /// <summary>A new roll: the cards still up go face down first, then the new faces turn up.</summary>
        public void Deal(IReadOnlyList<int> faces)
        {
            ThrowIfWrongLength(faces);
            CopyToDealt(faces);
            _dealPending = true;

            for (int i = 0; i < _cards.Length; i++)
                TurnTo(_cards[i], DiceCardFaces.FaceDown);

            TryFinishDeal();
        }

        /// <summary>Every card face down at once, without turning — the table is cleared for the next match.</summary>
        public void LayFaceDown()
        {
            _dealPending = false;

            foreach (Card card in _cards)
            {
                card.Turning = false;
                card.Pending = NoFace;
                card.Shown = card.Target = DiceCardFaces.FaceDown;
                card.Renderer.sprite = _art.GetSprite(DiceCardFaces.FaceDown);
            }
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>Moves every turn under way on by <paramref name="deltaTime"/> seconds. Unity calls it every frame.</summary>
        internal void Tick(float deltaTime)
        {
            foreach (Card card in _cards)
            {
                if (card.Turning)
                    Advance(card, deltaTime);
            }

            TryFinishDeal();
        }

        private void Advance(Card card, float deltaTime)
        {
            card.Elapsed += deltaTime;

            int frame = (int)(card.Elapsed / _art.FrameSeconds);
            if (frame < card.Frames.Count - 1)
            {
                card.Renderer.sprite = card.Frames[frame];
                return;
            }

            Finish(card);
        }

        private void TurnTo(Card card, int face)
        {
            if (card.Turning)
            {
                card.Pending = face == card.Target ? NoFace : face;
                return;
            }

            if (face == card.Shown)
                return;

            // The flips run between the back and a face, so one face to another goes through the back.
            if (card.Shown != DiceCardFaces.FaceDown && face != DiceCardFaces.FaceDown)
            {
                card.Pending = face;
                face = DiceCardFaces.FaceDown;
            }

            card.Target = face;
            card.Elapsed = 0f;
            card.Turning = true;
            CollectFrames(card);

            if (card.Frames.Count > 0)
                card.Renderer.sprite = card.Frames[0];

            // A single frame (or none, for missing art) is the whole turn.
            if (card.Frames.Count <= 1)
                Finish(card);
        }

        /// <summary>The frames from the face the card leaves to <see cref="Card.Target"/>, without the first.</summary>
        private void CollectFrames(Card card)
        {
            card.Frames.Clear();

            bool turningUp = card.Shown == DiceCardFaces.FaceDown;
            IReadOnlyList<Sprite> flip = _art.GetFlip(turningUp ? card.Target : card.Shown);
            if (flip == null)
                return;

            if (turningUp)
            {
                for (int i = 1; i < flip.Count; i++)
                    card.Frames.Add(flip[i]);
            }
            else
            {
                for (int i = flip.Count - 2; i >= 0; i--)
                    card.Frames.Add(flip[i]);
            }
        }

        private void Finish(Card card)
        {
            card.Turning = false;
            card.Shown = card.Target;
            card.Renderer.sprite = _art.GetSprite(card.Shown);

            if (card.Pending == NoFace)
                return;

            int next = card.Pending;
            card.Pending = NoFace;
            TurnTo(card, next);
        }

        private void TryFinishDeal()
        {
            if (!_dealPending)
                return;

            foreach (Card card in _cards)
            {
                if (card.Turning || card.Shown != DiceCardFaces.FaceDown)
                    return;
            }

            _dealPending = false;
            for (int i = 0; i < _cards.Length; i++)
                TurnTo(_cards[i], _dealt[i]);
        }

        private void CopyToDealt(IReadOnlyList<int> faces)
        {
            for (int i = 0; i < _dealt.Length; i++)
                _dealt[i] = faces[i];
        }

        private static void ThrowIfWrongLength(IReadOnlyList<int> faces)
        {
            if (faces == null || faces.Count != DiceCardFaces.CardCount)
                throw new ArgumentException($"Expected one face per card ({DiceCardFaces.CardCount}).", nameof(faces));
        }
    }
}
