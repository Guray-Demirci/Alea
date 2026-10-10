using System;
using System.Collections.Generic;
using UnityEngine;
using Tavla.Core.Checkers;

namespace Tavla.View
{
    /// <summary>
    /// How the player's special checkers look on the board, as an asset (Create > Tavla > Checker Art).
    /// Each kind brings only its resting art; whether it can move or is picked up is shown by a shared ring drawn
    /// around it, so a new checker needs a single sprite. A kind missing here looks like a plain checker.
    ///
    /// The art is keyed by the dropdown kind for the Inspector, and looked up by <see cref="BaseChecker.TypeId"/>
    /// on the board; <see cref="CheckerCatalog"/> connects the two.
    /// </summary>
    [CreateAssetMenu(fileName = "CheckerArt", menuName = "Tavla/Checker Art")]
    public sealed class CheckerArtSet : ScriptableObject
    {
        [Serializable]
        private sealed class Entry
        {
            public CheckerKind Checker;

            [Tooltip("The checker at rest, drawn in its own colours. Its pivot must sit on the centre of the disc.")]
            public Sprite Sprite;
        }

        [Tooltip("Drawn around a special checker while the pointer is over it and it can move with the rolled dice.")]
        [SerializeField] private Sprite _movableRing;

        [Tooltip("Drawn around a special checker while it is picked up.")]
        [SerializeField] private Sprite _selectedRing;

        [Tooltip("The art of each special checker kind. Each kind may be listed once.")]
        [SerializeField] private Entry[] _checkers = new Entry[0];

        // TypeId -> skin, built on first use; dropped whenever the asset is edited.
        private Dictionary<string, CheckerSkin> _skins;

        /// <summary>The skin for checkers of this type, or null if the type has no art here.</summary>
        public CheckerSkin GetSkin(string typeId)
        {
            if (_skins == null)
                _skins = BuildSkins();

            CheckerSkin skin;
            return typeId != null && _skins.TryGetValue(typeId, out skin) ? skin : null;
        }

        private Dictionary<string, CheckerSkin> BuildSkins()
        {
            Dictionary<string, CheckerSkin> skins = new Dictionary<string, CheckerSkin>();

            for (int i = 0; i < _checkers.Length; i++)
            {
                Entry entry = _checkers[i];
                if (entry == null || entry.Sprite == null)
                    continue;

                string typeId = CheckerCatalog.GetTypeId(entry.Checker);
                if (skins.ContainsKey(typeId))
                {
                    Debug.LogWarning($"[View] '{name}' lists {entry.Checker} twice; the first entry is used.", this);
                    continue;
                }

                skins.Add(typeId, new CheckerSkin
                {
                    Normal = entry.Sprite,
                    MovableRing = _movableRing,
                    SelectedRing = _selectedRing
                });
            }

            return skins;
        }

        private void OnValidate()
        {
            _skins = null;
        }
    }
}
