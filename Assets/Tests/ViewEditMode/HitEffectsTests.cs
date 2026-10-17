using System.Collections.Generic;
using NUnit.Framework;
using Tavla.Core.Board;
using Tavla.Core.Checkers;
using Tavla.Core.Runtime;
using UnityEngine;

namespace Tavla.View.Tests
{
    /// <summary>
    /// A hit as the screen shows it: the hit checker stands where it was until its capturer lands, breaks apart there,
    /// puts itself back together where Core sent it (the bar), and only then is its own visual seen again. The views here
    /// are placed the way BoardManager places them, without gliding: a test snaps a checker to say it has arrived.
    /// </summary>
    public class HitEffectsTests
    {
        private const float FrameTime = 0.1f;
        private const float Spacing = 0.5f;

        // Far from the 24 points, so the bar is never mistaken for one of them.
        private static readonly Vector3 BarOrigin = new Vector3(100f, 0f, 0f);

        private readonly Dictionary<BaseChecker, CheckerController> _views = new Dictionary<BaseChecker, CheckerController>();
        private readonly List<Object> _owned = new List<Object>();

        private Sprite _checkerSprite;
        private Sprite[] _frames;
        private Sprite[] _breakFrames;

        private GameState _board;
        private HitEffects _effects;
        private BaseChecker _capturer;
        private BaseChecker _victim;
        private int _target;
        private Vector3 _hitPoint;

        /// <summary>
        /// Two turns that end in a hit. Every starting stack holds two checkers or more, so the side to roll first opens
        /// one: it moves the top checker off a stack of two and leaves the other alone. Then the other side hits it.
        /// </summary>
        private struct Hit
        {
            public int Seed;
            public int OpenFirst, OpenSecond, OpenFrom, OpenTo;
            public int First, Second, From, Stack, To;
        }

        [SetUp]
        public void SetUp()
        {
            _checkerSprite = CreateSprite("Checker");
            _frames = new Sprite[5];
            for (int i = 0; i < _frames.Length; i++)
                _frames[i] = CreateSprite($"Break{i + 2}");

            _breakFrames = _frames;

            Hit hit = FindHit();
            _board = Deal(hit, true);
            _capturer = _board.Nodes[hit.From].Checkers[hit.Stack];
            _victim = _board.Nodes[hit.To].Checkers[0];
            _target = hit.To;

            for (int i = 0; i < GameState.NodeCount; i++)
            {
                for (int k = 0; k < _board.Nodes[i].CheckerCount; k++)
                    CreateView(_board.Nodes[i].Checkers[k]);
            }

            Place();
            foreach (CheckerController view in _views.Values)
                view.SnapToTarget();

            _hitPoint = _views[_victim].transform.position;

            _effects = new HitEffects(null, new ShakeStyle(), new ShakeStyle(), FrameTime,
                                      checker => _views.TryGetValue(checker, out CheckerController view) ? view : null,
                                      checker => _breakFrames);
            _effects.Attach(_board);
        }

        [TearDown]
        public void TearDown()
        {
            if (_effects != null)
                _effects.Detach();

            foreach (Object owned in _owned)
            {
                if (owned != null)
                    Object.DestroyImmediate(owned);
            }

            _owned.Clear();
            _views.Clear();
        }

        [Test]
        public void TheHitChecker_StandsWhereItWas_UntilTheCapturerLands()
        {
            _board.TryMove(_capturer, _target);
            Place();
            _effects.Tick(0.01f);

            Assert.IsFalse(_views[_victim].gameObject.activeSelf, "Its own visual is switched off.");
            Assert.AreEqual(_hitPoint, FindBreak().transform.position);
            Assert.AreSame(_checkerSprite, FindBreak().sprite, "It still stands whole.");

            _effects.Tick(1f);
            Assert.AreSame(_checkerSprite, FindBreak().sprite, "However long the capturer takes.");

            _views[_capturer].SnapToTarget();
            _effects.Tick(0f);

            Assert.AreSame(_frames[0], FindBreak().sprite, "It breaks as the capturer lands.");
        }

        [Test]
        public void TheBreak_PlaysWhereItWasHit_ThenBackwardsOnTheBar()
        {
            HitAndLand();

            Vector3 bar = VictimOnTheBar();
            Sprite[] sprites = { _frames[0], _frames[1], _frames[2], _frames[3], _frames[4],
                                 null, _frames[4], _frames[3], _frames[2], _frames[1], _frames[0] };

            for (int step = 0; step < sprites.Length; step++)
            {
                SpriteRenderer shown = FindBreak();
                Assert.IsNotNull(shown, $"step {step}");
                Assert.AreSame(sprites[step], shown.sprite, $"step {step}");
                Assert.AreEqual(step < _frames.Length ? _hitPoint : bar, shown.transform.position, $"step {step}");
                Assert.IsFalse(_views[_victim].gameObject.activeSelf, $"step {step}");

                _effects.Tick(FrameTime);
            }

            Assert.IsNull(FindBreak(), "The break is cleared away.");
            Assert.IsTrue(_views[_victim].gameObject.activeSelf, "Its own visual is back...");
            Assert.AreEqual(bar, _views[_victim].transform.position, "...on the bar.");
        }

        [Test]
        public void ACheckerWithoutBreakArt_IsNeverHidden()
        {
            _breakFrames = new Sprite[0];

            _board.TryMove(_capturer, _target);
            Place();
            _effects.Tick(0.01f);

            Assert.IsTrue(_views[_victim].gameObject.activeSelf);
            Assert.IsNull(FindBreak());
        }

        [Test]
        public void Detach_ClearsTheBreakAway_AndBringsTheCheckerBack()
        {
            HitAndLand();
            _effects.Tick(FrameTime);

            _effects.Detach();

            Assert.IsNull(FindBreak());
            Assert.IsTrue(_views[_victim].gameObject.activeSelf);
            Assert.AreEqual(VictimOnTheBar(), _views[_victim].transform.position);
        }

        /// <summary>Where <see cref="Place"/> puts the hit checker: alone on its side's bar.</summary>
        private Vector3 VictimOnTheBar()
        {
            return BarOrigin + new Vector3(_victim.OwnerId * Spacing, 0f, 0f);
        }

        private void HitAndLand()
        {
            _board.TryMove(_capturer, _target);
            Place();
            _effects.Tick(0f);

            _views[_capturer].SnapToTarget();
            _effects.Tick(0f);
        }

        /// <summary>Gives every view its place on the board as BoardManager does: point and stack, or the bar.</summary>
        private void Place()
        {
            foreach (KeyValuePair<BaseChecker, CheckerController> pair in _views)
            {
                int index = _board.FindNodeIndexOf(pair.Key);
                Vector3 position;

                if (index == GameState.BarIndex)
                {
                    IReadOnlyList<BaseChecker> bar = _board.GetBar(pair.Key.OwnerId);
                    position = BarOrigin + new Vector3(pair.Key.OwnerId * Spacing, IndexOf(bar, pair.Key) * Spacing, 0f);
                }
                else
                {
                    position = new Vector3(index, IndexOf(_board.Nodes[index].Checkers, pair.Key) * Spacing, 0f);
                }

                pair.Value.SetTarget(position, 0);
            }
        }

        private static int IndexOf(IReadOnlyList<BaseChecker> checkers, BaseChecker checker)
        {
            for (int i = 0; i < checkers.Count; i++)
            {
                if (checkers[i] == checker)
                    return i;
            }

            return -1;
        }

        private static SpriteRenderer FindBreak()
        {
            GameObject go = GameObject.Find("CheckerBreak");
            return go != null ? go.GetComponent<SpriteRenderer>() : null;
        }

        private void CreateView(BaseChecker checker)
        {
            GameObject go = new GameObject("Checker");
            _owned.Add(go);

            go.AddComponent<SpriteRenderer>().sprite = _checkerSprite;
            CheckerController view = go.AddComponent<CheckerController>();
            view.Bind(null, checker, null, Color.white);
            _views.Add(checker, view);
        }

        private Sprite CreateSprite(string name)
        {
            Texture2D texture = new Texture2D(2, 2);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f), 12f);
            sprite.name = name;
            _owned.Add(sprite);
            _owned.Add(texture);
            return sprite;
        }

        /// <summary>
        /// The first two turns that end in a hit on exactly one checker, the one left alone on the point it goes to.
        /// Searched, not written down, so a change to the starting formations cannot quietly turn the hit into a plain
        /// move.
        /// </summary>
        private static Hit FindHit()
        {
            List<int> openings = new List<int>();
            List<int> targets = new List<int>();

            for (int seed = 1; seed <= 50; seed++)
            for (int openFirst = 1; openFirst <= 6; openFirst++)
            for (int openSecond = 1; openSecond <= 6; openSecond++)
            {
                Hit hit = new Hit { Seed = seed, OpenFirst = openFirst, OpenSecond = openSecond };
                GameState start = Deal(hit, false);
                int opener = start.CurrentPlayerId;

                for (int from = 0; from < GameState.NodeCount; from++)
                {
                    Node stack = start.Nodes[from];
                    if (stack.CheckerCount != 2 || stack.OwnerId != opener)
                        continue;

                    start.GetLegalTargets(stack.Checkers[1], openings);
                    foreach (int openTo in openings)
                    {
                        hit.OpenFrom = from;
                        hit.OpenTo = openTo;
                        hit.First = hit.Second = 1;

                        Node blot = Deal(hit, true).Nodes[from];
                        if (blot.CheckerCount != 1 || blot.OwnerId != opener)
                            continue; // The opening did not leave it alone.

                        for (int first = 1; first <= 6; first++)
                        for (int second = 1; second <= 6; second++)
                        {
                            hit.First = first;
                            hit.Second = second;
                            GameState board = Deal(hit, true);

                            for (int capturer = 0; capturer < GameState.NodeCount; capturer++)
                            {
                                Node node = board.Nodes[capturer];
                                if (node.CheckerCount == 0 || node.OwnerId == opener)
                                    continue;

                                board.GetLegalTargets(node.Checkers[node.CheckerCount - 1], targets);
                                if (!targets.Contains(from))
                                    continue;

                                hit.From = capturer;
                                hit.Stack = node.CheckerCount - 1;
                                hit.To = from;
                                if (HitsOnlyThatChecker(hit, opener))
                                    return hit;
                            }
                        }
                    }
                }
            }

            Assert.Fail("No two turns end in a hit on a lone checker.");
            return default;
        }

        private static bool HitsOnlyThatChecker(Hit hit, int opener)
        {
            GameState board = Deal(hit, true);
            BaseChecker victim = board.Nodes[hit.To].Checkers[0];

            if (!board.TryMove(board.Nodes[hit.From].Checkers[hit.Stack], hit.To))
                return false;

            IReadOnlyList<BaseChecker> bar = board.GetBar(opener);
            return bar.Count == 1 && bar[0] == victim;
        }

        /// <summary>
        /// Plain checkers for both sides (the smallest count, stacks of two and three), dealt with the seed; the side to
        /// roll first holds the opening roll. With <paramref name="opened"/> it has played the opening and the other side
        /// holds the hitting roll.
        /// </summary>
        private static GameState Deal(Hit hit, bool opened)
        {
            const int count = GameState.MinCheckersPerPlayer;

            GameState board = new GameState();
            SystemRandomProvider random = new SystemRandomProvider(hit.Seed);
            MatchContext context = new MatchContext(board, random);
            LoadedDice[] dice = new LoadedDice[GameState.PlayerCount];

            StartingLayout layout = StartingLayout.Deal(count, count, random);
            for (int player = 0; player < GameState.PlayerCount; player++)
            {
                BaseChecker[] checkers = new BaseChecker[count];
                for (int i = 0; i < checkers.Length; i++)
                    checkers[i] = new DefaultChecker(player);

                board.SetupPlayer(player, checkers, layout);

                dice[player] = new LoadedDice();
                context.RegisterDiceModifier(player, dice[player]);
            }

            int opener = board.CurrentPlayerId;
            dice[opener].Load(hit.OpenFirst, hit.OpenSecond);
            context.RollDice(opener);

            if (!opened)
                return board;

            board.TryMove(board.Nodes[hit.OpenFrom].Checkers[1], hit.OpenTo);
            board.EndTurn();

            int hitter = board.CurrentPlayerId;
            dice[hitter].Load(hit.First, hit.Second);
            context.RollDice(hitter);
            return board;
        }
    }
}
