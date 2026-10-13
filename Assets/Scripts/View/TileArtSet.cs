using System;
using System.Collections.Generic;
using UnityEngine;
using Tavla.Core.Interfaces;

namespace Tavla.View
{
    /// <summary>
    /// How the tiles look on the board's points, as an asset (Create > Tavla > Tile Art). A tile is drawn as its point
    /// turned into it: the whole triangle, 10x56, pivoted on its centre, which is where
    /// <see cref="BoardLayout.GetNodeCenter"/> puts it. The art is drawn for the bottom row (base down); the top row
    /// shows it turned half round, as points.png does with its triangles. A kind missing here leaves its point plain.
    /// A tile that opens and closes (Dungeon) looks open on one frame of its transition (<see cref="GetOpenFrame"/>) and
    /// closed at rest.
    ///
    /// The art is keyed by the dropdown kind for the Inspector, and looked up by <see cref="ITileEffect.EffectId"/>
    /// on the board; <see cref="TileCatalog"/> connects the two.
    /// </summary>
    [CreateAssetMenu(fileName = "TileArt", menuName = "Tavla/Tile Art")]
    public sealed class TileArtSet : ScriptableObject
    {
        [Serializable]
        private sealed class Entry
        {
            public TileKind Tile;

            [Tooltip("The point with the tile on it, at rest.")]
            public Sprite Point;

            [Tooltip("A light point turning into the tile: the frames between the plain point and the tile at rest, " +
                     "first to last. Played backwards when the tile leaves.")]
            public Sprite[] FromLight = new Sprite[0];

            [Tooltip("The same for a dark point. Empty = on a dark point the tile appears at once.")]
            public Sprite[] FromDark = new Sprite[0];

            [Tooltip("For a tile that opens and closes (Dungeon): which frame of From Light / From Dark shows it open, " +
                     "counting from 1. The frames after it close it; the Point is the tile closed. " +
                     "0 = the tile always looks the same.")]
            [Min(0)] public int OpenFrame;
        }

        [Tooltip("The art of each tile kind. Each kind may be listed once.")]
        [SerializeField] private Entry[] _tiles = new Entry[0];

        [Tooltip("How long each frame of a point turning into a tile is shown. The artist's files use 100 ms.")]
        [SerializeField, Min(0.01f)] private float _frameSeconds = 0.1f;

        // EffectId -> entry, built on first use; dropped whenever the asset is edited.
        private Dictionary<string, Entry> _byEffectId;

        public float FrameSeconds => _frameSeconds;

        /// <summary>
        /// Does every kind listed here have its point and both transitions, with no frame missing, and an open frame
        /// both transitions have?
        /// </summary>
        public bool IsComplete
        {
            get
            {
                if (_tiles == null)
                    return false;

                foreach (Entry entry in _tiles)
                {
                    if (entry == null || entry.Point == null || !IsWhole(entry.FromLight) || !IsWhole(entry.FromDark))
                        return false;

                    if (entry.OpenFrame > entry.FromLight.Length || entry.OpenFrame > entry.FromDark.Length)
                        return false;
                }

                return true;
            }
        }

        /// <summary>The point with this tile on it, at rest; null for no tile, or a tile without art.</summary>
        public Sprite GetSprite(string effectId)
        {
            Entry entry = Find(effectId);
            return entry != null ? entry.Point : null;
        }

        /// <summary>
        /// The frames between a plain point of this colour and the tile at rest, first to last; null for no tile, or a
        /// tile without art.
        /// </summary>
        public IReadOnlyList<Sprite> GetTransition(string effectId, bool fromLight)
        {
            Entry entry = Find(effectId);
            if (entry == null)
                return null;

            return fromLight ? entry.FromLight : entry.FromDark;
        }

        /// <summary>
        /// For a tile that opens and closes: which frame of its transitions shows it open, counting from 1; the frames
        /// after it close it. 0 for a tile that always looks the same, no tile, or a tile without art.
        /// </summary>
        public int GetOpenFrame(string effectId)
        {
            Entry entry = Find(effectId);
            return entry != null ? entry.OpenFrame : 0;
        }

        private Entry Find(string effectId)
        {
            if (effectId == null)
                return null;

            if (_byEffectId == null)
                _byEffectId = BuildLookup();

            Entry entry;
            return _byEffectId.TryGetValue(effectId, out entry) ? entry : null;
        }

        private Dictionary<string, Entry> BuildLookup()
        {
            Dictionary<string, Entry> lookup = new Dictionary<string, Entry>();

            for (int i = 0; i < _tiles.Length; i++)
            {
                Entry entry = _tiles[i];
                if (entry == null || entry.Point == null)
                    continue;

                string effectId = TileCatalog.GetEffectId(entry.Tile);
                if (lookup.ContainsKey(effectId))
                {
                    Debug.LogWarning($"[View] '{name}' lists {entry.Tile} twice; the first entry is used.", this);
                    continue;
                }

                lookup.Add(effectId, entry);
            }

            return lookup;
        }

        private static bool IsWhole(Sprite[] frames)
        {
            if (frames == null)
                return false;

            foreach (Sprite frame in frames)
            {
                if (frame == null)
                    return false;
            }

            return true;
        }

        private void OnValidate()
        {
            _byEffectId = null;
        }
    }
}
