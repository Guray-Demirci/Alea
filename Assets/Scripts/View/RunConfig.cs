using System;
using UnityEngine;
using Tavla.Core.Board;
using Tavla.Core.Interfaces;
using Tavla.Core.Relics;
using Tavla.Core.Run;

namespace Tavla.View
{
    /// <summary>
    /// The design data of a run — its stages, their matches and tiles, the player's starting deck, the
    /// economy and the shop — as an asset, so that balancing needs no recompile. Only DATA lives here:
    /// <see cref="ToDefinition"/> turns it into Core's <see cref="RunDefinition"/>, which validates it and is
    /// what the game actually plays.
    ///
    /// Points are board indices 0-23, the same numbers the logs print. Player 0 moves from 0 towards 23.
    /// </summary>
    [CreateAssetMenu(fileName = "Run", menuName = "Tavla/Run")]
    public sealed class RunConfig : ScriptableObject
    {
        [Serializable]
        private sealed class MatchEntry
        {
            [Tooltip("Score the player must reach to win the match.")]
            [Min(1)] public int TargetScore;

            [Tooltip("The player's turns to reach the target in. The opponent's turns are not counted.")]
            [Min(1)] public int TurnLimit;

            [Tooltip("Checkers the opponent starts with, dealt in one of the StartingPatterns formations.")]
            [Range(GameState.MinCheckersPerPlayer, GameState.MaxCheckersPerPlayer)]
            public int OpponentCheckers = GameState.MaxCheckersPerPlayer;

            [Tooltip("A boss (challenge) match pays the boss reward instead of the normal one.")]
            public bool IsBoss;
        }

        [Serializable]
        private sealed class TileEntry
        {
            public TileKind Tile;

            [Tooltip("Board point index 0-23, as printed in the logs.")]
            [Range(0, GameState.NodeCount - 1)] public int Point;
        }

        [Serializable]
        private sealed class ShopCheckerEntry
        {
            public CheckerKind Checker;

            [Tooltip("Rarity tier: 1 common .. 4 special. Sets the price; a luxury shop (Phoenix) skips tier 1.")]
            [Range(ShopItem.MinTier, ShopItem.MaxTier)] public int Tier = ShopItem.MinTier;
        }

        [Serializable]
        private sealed class StageEntry
        {
            [Tooltip("The matches of the stage, in play order.")]
            public MatchEntry[] Matches = new MatchEntry[0];

            [Tooltip("Tiles this stage adds. They stay on the board for the rest of the run; " +
                     "a tile on a point that already holds one replaces it.")]
            public TileEntry[] NewTiles = new TileEntry[0];

            [Tooltip("Tiles drawn at random from the Tile Pool when the run reaches this stage, each on a random point " +
                     "no stage places a tile on. They stay for the rest of the run. A tile comes at most once a run.")]
            [Min(0)] public int RandomTiles;

            [Tooltip("How likely tier 1, 2, 3 and 4 are on a special checker shelf in the shops before this stage's " +
                     "matches (the one after the previous boss included). Relative weights: 70, 25, 5, 0 = 70% / 25% / " +
                     "5% / never. Empty = every special checker equally likely.")]
            public int[] ShopTierOdds = new int[0];
        }

        [Header("Player")]
        [Tooltip("Default checkers in the player's deck when the run begins. Every checker bought in the shop is added.")]
        [SerializeField, Range(GameState.MinCheckersPerPlayer, GameState.MaxCheckersPerPlayer)]
        private int _startingCheckers = GameState.MinCheckersPerPlayer;

        [Header("Rules")]
        [Tooltip("Turns left to the player once the opponent runs out of checkers. 0 = the match ends.")]
        [SerializeField, Min(0)] private int _finalStretchTurns = 3;

        [Header("Economy")]
        [Tooltip("Gold the player holds when the run begins.")]
        [SerializeField, Min(0)] private int _startingGold;
        [Tooltip("Gold paid for winning a normal match.")]
        [SerializeField, Min(0)] private int _winGold;
        [Tooltip("Gold paid for winning a boss (challenge) match.")]
        [SerializeField, Min(0)] private int _bossWinGold;
        [Tooltip("Gold paid on a win for every turn of the limit the player did not need.")]
        [SerializeField, Min(0)] private int _goldPerUnusedTurn;

        [Header("Stages")]
        [Tooltip("The stages in play order.")]
        [SerializeField] private StageEntry[] _stages = new StageEntry[0];

        [Header("Tiles")]
        [Tooltip("Tiles a stage's Random Tiles are drawn from. Each may be listed once; a tile already on the board " +
                 "this run — placed by a stage or drawn — is never drawn again.")]
        [SerializeField] private TileKind[] _tilePool = new TileKind[0];

        [Header("Shop: items")]
        [Tooltip("Special checkers the shop may offer, with their tier. Each may be listed once.")]
        [SerializeField] private ShopCheckerEntry[] _shopCheckers = new ShopCheckerEntry[0];
        [Tooltip("Single-use cards the shop may offer.")]
        [SerializeField] private CardKind[] _shopCards = new CardKind[0];
        [Tooltip("Permanent dice charms the shop may offer. One the player owns is never offered again.")]
        [SerializeField] private CharmKind[] _shopCharms = new CharmKind[0];
        [Tooltip("Most special checkers the deck may hold.")]
        [SerializeField, Range(0, GameState.MaxCheckersPerPlayer)]
        private int _maxSpecialCheckers = ShopCatalog.DefaultMaxSpecialCheckers;

        [Header("Shop: prices")]
        [Tooltip("A plain checker (+1 to the deck).")]
        [SerializeField, Min(0)] private int _basicCheckerPrice = 2;
        [Tooltip("Special checker price for tier 1, 2, 3 and 4.")]
        [SerializeField] private int[] _tierPrices = { 4, 6, 8, 10 };
        [SerializeField, Min(0)] private int _cardPrice = 4;
        [SerializeField, Min(0)] private int _charmPrice = 15;
        [Tooltip("Cost of each reroll within one shop visit; the count is how many rerolls a visit allows.")]
        [SerializeField] private int[] _rerollPrices = { 1, 3, 5, 7, 9 };

        /// <summary>Builds the run Core plays. Validation is Core's; errors are prefixed with where they are in this asset.</summary>
        /// <exception cref="ArgumentException">The data is invalid.</exception>
        public RunDefinition ToDefinition()
        {
            StageDefinition[] stages = new StageDefinition[_stages.Length];
            for (int s = 0; s < stages.Length; s++)
                stages[s] = BuildStage(_stages[s], s);

            Func<ITileEffect>[] tilePool = new Func<ITileEffect>[_tilePool.Length];
            for (int i = 0; i < tilePool.Length; i++)
                tilePool[i] = TileCatalog.GetFactory(_tilePool[i]);

            RunEconomy economy = new RunEconomy(_startingGold, _winGold, _bossWinGold, _goldPerUnusedTurn);
            return new RunDefinition(stages, _startingCheckers, _finalStretchTurns, economy, BuildShop(), tilePool);
        }

        private ShopCatalog BuildShop()
        {
            ShopItem[] checkers = new ShopItem[_shopCheckers.Length];
            for (int i = 0; i < checkers.Length; i++)
                checkers[i] = CreateShopChecker(_shopCheckers[i].Checker, _shopCheckers[i].Tier);

            ShopItem[] cards = new ShopItem[_shopCards.Length];
            for (int i = 0; i < cards.Length; i++)
                cards[i] = CreateCard(_shopCards[i]);

            ShopItem[] charms = new ShopItem[_shopCharms.Length];
            for (int i = 0; i < charms.Length; i++)
                charms[i] = CreateCharm(_shopCharms[i]);

            try
            {
                ShopPrices prices = new ShopPrices(_basicCheckerPrice, _tierPrices, _cardPrice, _charmPrice, _rerollPrices);
                return new ShopCatalog(checkers, cards, charms, prices, _maxSpecialCheckers);
            }
            catch (ArgumentException e)
            {
                throw new ArgumentException($"Shop: {e.Message}", e);
            }
        }

        private static StageDefinition BuildStage(StageEntry entry, int stageIndex)
        {
            string where = $"Stages[{stageIndex}]";

            MatchDefinition[] matches = new MatchDefinition[entry.Matches.Length];
            for (int m = 0; m < matches.Length; m++)
            {
                MatchEntry match = entry.Matches[m];
                try
                {
                    matches[m] = new MatchDefinition(match.TargetScore, match.TurnLimit, match.OpponentCheckers, match.IsBoss);
                }
                catch (ArgumentException e)
                {
                    throw new ArgumentException($"{where}.Matches[{m}]: {e.Message}", e);
                }
            }

            TilePlacement[] tiles = new TilePlacement[entry.NewTiles.Length];
            for (int t = 0; t < tiles.Length; t++)
            {
                try
                {
                    tiles[t] = TileCatalog.CreatePlacement(entry.NewTiles[t].Tile, entry.NewTiles[t].Point);
                }
                catch (ArgumentException e)
                {
                    throw new ArgumentException($"{where}.NewTiles[{t}]: {e.Message}", e);
                }
            }

            ShopTierOdds odds = null;
            if (entry.ShopTierOdds != null && entry.ShopTierOdds.Length > 0)
            {
                try
                {
                    odds = new ShopTierOdds(entry.ShopTierOdds);
                }
                catch (ArgumentException e)
                {
                    throw new ArgumentException($"{where}.ShopTierOdds: {e.Message}", e);
                }
            }

            try
            {
                return new StageDefinition(matches, tiles, odds, entry.RandomTiles);
            }
            catch (ArgumentException e)
            {
                throw new ArgumentException($"{where}: {e.Message}", e);
            }
        }

        /// <summary>The checker classes behind the dropdown live in <see cref="CheckerCatalog"/>.</summary>
        private static ShopItem CreateShopChecker(CheckerKind kind, int tier)
        {
            return ShopItem.SpecialChecker(tier, CheckerCatalog.GetFactory(kind));
        }

        private static ShopItem CreateCard(CardKind kind)
        {
            switch (kind)
            {
                case CardKind.MeridianShift: return ShopItem.Card(() => new MeridianShiftRelic());
                case CardKind.CartographersCheat: return ShopItem.Card(() => new CartographersCheatRelic());
                default: throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown card kind.");
            }
        }

        private static ShopItem CreateCharm(CharmKind kind)
        {
            switch (kind)
            {
                case CharmKind.TwinWinds: return ShopItem.Charm(() => new TwinWindsRelic());
                default: throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown charm kind.");
            }
        }

        /// <summary>Checks the asset without entering Play mode (the ⋮ menu at the top of the asset's Inspector).</summary>
        [ContextMenu("Validate")]
        private void Validate()
        {
            try
            {
                RunDefinition run = ToDefinition();
                GameLog.Info($"[View] Run '{name}' is valid: {run.Stages.Count} stages, {run.MatchCount} matches.", this);
            }
            catch (ArgumentException e)
            {
                Debug.LogError($"[View] Run '{name}' is invalid: {e.Message}", this);
            }
        }
    }
}
