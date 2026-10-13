using System;
using System.Collections.Generic;
using UnityEngine;
using Tavla.Core.Board;

namespace Tavla.View
{
    /// <summary>
    /// The tiles on the board's points. DUMB PUPPET like <see cref="DiceCardsView"/>: it does not know the match —
    /// BoardManager tells it which tile each point carries, by <see cref="Tavla.Core.Interfaces.ITileEffect.EffectId"/>;
    /// it only knows where the points are and how a point turns into a tile.
    ///
    /// A point that gains a tile plays the artist's transition from its own colour
    /// (<see cref="TileArtSet.GetTransition"/>), one that loses it plays the transition backwards, and one tile giving
    /// way to another goes through the plain point. A tile without art leaves its point plain. A change asked for
    /// while another is under way cuts that one short.
    ///
    /// A tile that opens and closes (<see cref="Tavla.Core.Interfaces.IOpenCloseTile"/>, the Dungeon) rests on one frame
    /// of its transition while open (<see cref="TileArtSet.GetOpenFrame"/>) and on its point while closed: closing plays
    /// the frames between the two, opening plays them backwards.
    ///
    /// A tile that counts down (<see cref="Tavla.Core.Interfaces.ICountdownTile"/>, the Powder Keg) also shows its count
    /// near the tip: a placeholder until the artist draws one, in the fuse's yellow on the keg's outline colour.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TilesView : MonoBehaviour
    {
        private sealed class Slot
        {
            public SpriteRenderer Renderer;

            /// <summary>The colour of the plain point under the tile, which its transition starts from.</summary>
            public bool Light;

            /// <summary>The tile the point rests on, or is turning into; null = the plain point.</summary>
            public string Target;

            /// <summary>Is <see cref="Target"/> shown open? Only a tile with an open frame looks any different.</summary>
            public bool TargetOpen;

            public bool Changing;
            public float Elapsed;

            /// <summary>The frames of the change under way; the last is <see cref="Target"/> at rest.</summary>
            public readonly List<Sprite> Frames = new List<Sprite>();

            /// <summary>The countdown badge; hidden while the point shows no count.</summary>
            public GameObject Count;
            public PixelLabel CountLabel;
        }

        /// <summary>Passed to <see cref="ShowCount"/> for a point that shows no count.</summary>
        public const int NoCount = 0;

        // Taken from the keg: the outline of its point and the spark of its fuse.
        private static readonly Color CountBadgeColor = new Color32(0x24, 0x1B, 0x1C, 0xFF);
        private static readonly Color CountDigitColor = new Color32(0xCF, 0xC8, 0x30, 0xFF);

        private readonly Slot[] _slots = new Slot[GameState.NodeCount];

        private TileArtSet _art;

        /// <summary>Builds one tile slot over every point in code, all of them plain and without a count.</summary>
        /// <param name="solid">A 1x1 white sprite of 1 world unit, for the countdown badges.</param>
        public static TilesView Create(TileArtSet art, Sprite solid, BoardLayout layout)
        {
            if (art == null)
                throw new ArgumentNullException(nameof(art));

            if (solid == null)
                throw new ArgumentNullException(nameof(solid));

            if (layout == null)
                throw new ArgumentNullException(nameof(layout));

            TilesView view = BoardSceneBuilder.CreateWorldRoot("Tiles").gameObject.AddComponent<TilesView>();
            view.Build(art, solid, layout);
            return view;
        }

        private void Build(TileArtSet art, Sprite solid, BoardLayout layout)
        {
            _art = art;

            for (int i = 0; i < _slots.Length; i++)
            {
                GameObject go = new GameObject($"Tile_{i:00}");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = layout.GetNodeCenter(i);

                // The art is drawn for the bottom row; the top row turns it half round, as points.png does.
                SpriteRenderer spriteRenderer = go.AddComponent<SpriteRenderer>();
                spriteRenderer.sortingOrder = SortingOrders.Tiles;
                spriteRenderer.flipX = spriteRenderer.flipY = layout.IsTopRow(i);

                _slots[i] = new Slot { Renderer = spriteRenderer, Light = layout.IsLightPoint(i) };
                BuildCount(_slots[i], go.transform, i, solid, layout);
            }
        }

        private static void BuildCount(Slot slot, Transform tile, int point, Sprite solid, BoardLayout layout)
        {
            slot.Count = new GameObject("Count");
            slot.Count.transform.SetParent(tile, false);
            slot.Count.transform.position = layout.GetTileCountPosition(point);

            GameObject badge = new GameObject("Badge");
            badge.transform.SetParent(slot.Count.transform, false);
            badge.transform.localScale = layout.GetTileCountSize();

            SpriteRenderer badgeRenderer = badge.AddComponent<SpriteRenderer>();
            badgeRenderer.sprite = solid;
            badgeRenderer.color = CountBadgeColor;
            badgeRenderer.sortingOrder = SortingOrders.TileCount;

            GameObject digit = new GameObject("Digit");
            digit.transform.SetParent(slot.Count.transform, false);

            slot.CountLabel = digit.AddComponent<PixelLabel>();
            slot.CountLabel.Configure(layout.PixelsPerUnit, 0, SortingOrders.TileCount + 1, CountDigitColor,
                                      PixelTextAlign.Center);

            slot.Count.SetActive(false);
        }

        /// <summary>The point turns into this tile; null turns it back into the plain point.</summary>
        /// <param name="point">The point index, 0-23.</param>
        /// <param name="effectId">The tile, or null for none.</param>
        /// <param name="open">Is the tile open (<see cref="Tavla.Core.Interfaces.IOpenCloseTile"/>)? Closed otherwise.</param>
        /// <exception cref="ArgumentOutOfRangeException">A point off the board.</exception>
        public void Show(int point, string effectId, bool open = false)
        {
            Slot slot = GetSlot(point);
            if (effectId == slot.Target && open == slot.TargetOpen)
                return;

            if (slot.Changing)
                Finish(slot);

            CollectFrames(slot, slot.Target, slot.TargetOpen, effectId, open);
            slot.Target = effectId;
            slot.TargetOpen = open;
            slot.Elapsed = 0f;
            slot.Changing = true;
            slot.Renderer.sprite = slot.Frames[0];

            // A tile without a transition (or without art) is there at once.
            if (slot.Frames.Count == 1)
                Finish(slot);
        }

        /// <summary>Shows a tile's countdown on the point; <see cref="NoCount"/> (or less) hides it.</summary>
        /// <param name="point">The point index, 0-23.</param>
        /// <exception cref="ArgumentOutOfRangeException">A point off the board.</exception>
        public void ShowCount(int point, int count)
        {
            Slot slot = GetSlot(point);

            bool shown = count > NoCount;
            if (shown)
                slot.CountLabel.SetText(count.ToString());

            if (slot.Count.activeSelf != shown)
                slot.Count.SetActive(shown);
        }

        private Slot GetSlot(int point)
        {
            if (point < 0 || point >= _slots.Length)
                throw new ArgumentOutOfRangeException(nameof(point), point, "A tile lies on a point, 0-23.");

            return _slots[point];
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>Moves every change under way on by <paramref name="deltaTime"/> seconds. Unity calls it every frame.</summary>
        internal void Tick(float deltaTime)
        {
            foreach (Slot slot in _slots)
            {
                if (slot.Changing)
                    Advance(slot, deltaTime);
            }
        }

        private void Advance(Slot slot, float deltaTime)
        {
            slot.Elapsed += deltaTime;

            int frame = (int)(slot.Elapsed / _art.FrameSeconds);
            if (frame < slot.Frames.Count - 1)
            {
                slot.Renderer.sprite = slot.Frames[frame];
                return;
            }

            Finish(slot);
        }

        /// <summary>
        /// A tile's frames on a point line up as its transition and then the tile at rest (closed). Each look of the tile
        /// is a stop on that line: closed is the last, open is its open frame. From one tile to another: back along the
        /// first one's line, the plain point for a frame, along the second one's, then the second tile as it rests. The
        /// same tile opening or closing only moves between its two stops. A null sprite is the plain point, since the
        /// overlay then hides nothing.
        /// </summary>
        private void CollectFrames(Slot slot, string from, bool fromOpen, string to, bool toOpen)
        {
            slot.Frames.Clear();

            int stop = GetStop(to, toOpen, slot.Light);

            if (from == to)
            {
                int start = GetStop(from, fromOpen, slot.Light);
                for (int i = start + 1; i < stop; i++)
                    slot.Frames.Add(GetFrame(to, i, slot.Light));

                for (int i = start - 1; i > stop; i--)
                    slot.Frames.Add(GetFrame(to, i, slot.Light));
            }
            else
            {
                for (int i = GetStop(from, fromOpen, slot.Light) - 1; i >= 0; i--)
                    slot.Frames.Add(GetFrame(from, i, slot.Light));

                if (_art.GetTransition(from, slot.Light) != null && _art.GetTransition(to, slot.Light) != null)
                    slot.Frames.Add(null);

                for (int i = 0; i < stop; i++)
                    slot.Frames.Add(GetFrame(to, i, slot.Light));
            }

            slot.Frames.Add(GetFrame(to, stop, slot.Light));
        }

        /// <summary>
        /// Where on the tile's line it rests: its open frame while open, if it has one this colour can show; the end
        /// (the tile closed) otherwise. 0 for no tile or a tile without art.
        /// </summary>
        private int GetStop(string effectId, bool open, bool light)
        {
            IReadOnlyList<Sprite> transition = _art.GetTransition(effectId, light);
            if (transition == null)
                return 0;

            int openFrame = _art.GetOpenFrame(effectId);
            return open && openFrame > 0 && openFrame <= transition.Count ? openFrame - 1 : transition.Count;
        }

        /// <summary>The tile's frame at this place on its line: a transition frame, or past them the tile closed.</summary>
        private Sprite GetFrame(string effectId, int index, bool light)
        {
            IReadOnlyList<Sprite> transition = _art.GetTransition(effectId, light);
            return transition != null && index < transition.Count ? transition[index] : _art.GetSprite(effectId);
        }

        private void Finish(Slot slot)
        {
            slot.Changing = false;
            slot.Renderer.sprite = GetFrame(slot.Target, GetStop(slot.Target, slot.TargetOpen, slot.Light), slot.Light);
        }
    }
}
