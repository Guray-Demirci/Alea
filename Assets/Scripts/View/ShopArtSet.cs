using System;
using UnityEngine;
using Tavla.Core.Run;

namespace Tavla.View
{
    /// <summary>
    /// How the shop looks, as an asset (Create > Tavla > Shop Art). The sprites are cut from the artist's scheme
    /// in_game_shop_ready.aseprite and the shelf files tierNyastık_wprice.aseprite; <see cref="ShopView"/> knows
    /// where each one goes.
    /// </summary>
    [CreateAssetMenu(fileName = "ShopArt", menuName = "Tavla/Shop Art")]
    public sealed class ShopArtSet : ScriptableObject
    {
        /// <summary>Special checker tiers, so one cushion each.</summary>
        public const int TierCount = ShopItem.MaxTier - ShopItem.MinTier + 1;

        [Tooltip("The Merchant Table with the NEXT button and the plain checker's cushion (scheme layers masa, Next " +
                 "and standartpul), on a full 320x180 canvas.")]
        [SerializeField] private Sprite _table;

        [Tooltip("The ROLL button with its price tag (scheme layer Roll), on a full 320x180 canvas of its own so it " +
                 "can grey out when no reroll is left.")]
        [SerializeField] private Sprite _rollButton;

        [Tooltip("A special checker shelf per tier, from tier 1 to tier 4: the cushion in the tier's colour with its " +
                 "price tag. The pivot must sit on the centre of the cushion's disc, where the checker lies.")]
        [SerializeField] private Sprite[] _tierCushions = new Sprite[TierCount];

        public Sprite Table => _table;
        public Sprite RollButton => _rollButton;

        /// <summary>Does every sprite the shop draws have art?</summary>
        public bool IsComplete
        {
            get
            {
                if (_table == null || _rollButton == null || _tierCushions == null || _tierCushions.Length != TierCount)
                    return false;

                foreach (Sprite cushion in _tierCushions)
                {
                    if (cushion == null)
                        return false;
                }

                return true;
            }
        }

        /// <summary>The shelf of a tier (<see cref="ShopItem.MinTier"/>..<see cref="ShopItem.MaxTier"/>); null if it has no art.</summary>
        public Sprite GetCushion(int tier)
        {
            int index = tier - ShopItem.MinTier;
            return _tierCushions != null && index >= 0 && index < _tierCushions.Length ? _tierCushions[index] : null;
        }

        private void OnValidate()
        {
            // Exactly one cushion per tier, so the list index always matches the tier.
            if (_tierCushions == null || _tierCushions.Length != TierCount)
                Array.Resize(ref _tierCushions, TierCount);
        }
    }
}
