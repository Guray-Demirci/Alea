using NUnit.Framework;
using Tavla.Core.Board;
using Tavla.Core.Checkers;
using Tavla.Core.Runtime;
using UnityEditor;
using UnityEngine;

namespace Tavla.View.Tests
{
    /// <summary>Whose rolls turn the dice cards up: the human's do, the bot's leave them face down.</summary>
    public class DiceCardsPresenterTests
    {
        private const int Human = 0;
        private const int Bot = 1;

        private DiceCardArtSet _art;
        private BoardLayout _layout;
        private DiceCardsView _view;
        private GameState _board;
        private MatchContext _context;
        private DiceCardsPresenter _presenter;
        private bool _botPlaying;

        // A whole flip and half a frame more (see DiceCardsViewTests).
        private float Turn => 4.5f * _art.FrameSeconds;

        [SetUp]
        public void SetUp()
        {
            _art = AssetDatabase.LoadAssetAtPath<DiceCardArtSet>(DataAssetTests.DiceCardArtPath);
            _layout = BoardLayout.CreateDefault();
            _view = DiceCardsView.Create(_art, _layout);

            _board = new GameState();
            SystemRandomProvider random = new SystemRandomProvider(1);
            _context = new MatchContext(_board, random);

            StartingLayout layout = StartingLayout.Deal(GameState.MaxCheckersPerPlayer, GameState.MaxCheckersPerPlayer, random);
            for (int player = 0; player < GameState.PlayerCount; player++)
            {
                BaseChecker[] checkers = new BaseChecker[GameState.MaxCheckersPerPlayer];
                for (int i = 0; i < checkers.Length; i++)
                    checkers[i] = new DefaultChecker(player);

                _board.SetupPlayer(player, checkers, layout);

                // Every roll is 5-2.
                LoadedDice dice = new LoadedDice();
                dice.Load(5, 2);
                _context.RegisterDiceModifier(player, dice);
            }

            _botPlaying = true;
            _presenter = new DiceCardsPresenter(_view, player => !_botPlaying || player != Bot);
            _presenter.Bind(_board, _context);
        }

        [TearDown]
        public void TearDown()
        {
            _presenter.Unbind();
            Object.DestroyImmediate(_view.gameObject);
            Object.DestroyImmediate(_layout);
        }

        [Test]
        public void TheHumansRoll_TurnsTheCardsUp()
        {
            _context.RollDice(Human);
            _view.Tick(Turn);

            AssertFaces(0, 2, 0, 0, 5, 0);
        }

        [Test]
        public void TheBotsRoll_LeavesThemDown_AndTurnsDownTheHumansLeftovers()
        {
            _context.RollDice(Human);
            _view.Tick(Turn);

            _board.EndTurn();
            Assert.AreEqual(Bot, _board.CurrentPlayerId);

            _context.RollDice(Bot);
            _view.Tick(Turn);
            _view.Tick(Turn);

            AssertFaces(0, 0, 0, 0, 0, 0);
        }

        [Test]
        public void WithTheBotOff_EveryRollIsShown()
        {
            _botPlaying = false;

            _board.EndTurn();
            _context.RollDice(Bot);
            _view.Tick(Turn);

            AssertFaces(0, 2, 0, 0, 5, 0);
        }

        [Test]
        public void TurningTheBotOff_MidTurn_TurnsItsDiceUp()
        {
            _board.EndTurn();
            _context.RollDice(Bot);
            _view.Tick(Turn);

            _botPlaying = false;
            _presenter.Refresh();
            _view.Tick(Turn);

            AssertFaces(0, 2, 0, 0, 5, 0);
        }

        private void AssertFaces(params int[] faces)
        {
            for (int i = 0; i < faces.Length; i++)
            {
                Sprite shown = _view.transform.GetChild(i).GetComponent<SpriteRenderer>().sprite;
                Assert.AreSame(_art.GetSprite(faces[i]), shown, $"Card {i + 1}.");
            }
        }
    }
}
