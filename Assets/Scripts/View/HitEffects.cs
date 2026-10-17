using System;
using System.Collections.Generic;
using UnityEngine;
using Tavla.Core.Abilities;
using Tavla.Core.Board;
using Tavla.Core.Checkers;
using Tavla.Core.Runtime;

namespace Tavla.View
{
    /// <summary>
    /// What a hit looks like. The screen shakes, and a checker whose skin has break art (<see cref="CheckerSkin.Break"/>:
    /// both sides' plain checkers and every special one, each with its own) breaks apart where it stood. Both happen at
    /// the moment of impact:
    ///   • a hit made by a move: as the capturing checker LANDS. Core reports the capture as the move is made and the
    ///     View only then glides the checker there, so reacting to the report would react while it is on its way;
    ///   • a checker the Maelstrom throws onto a made point: as it reaches that point, where it breaks;
    ///   • a hit no move brought (a volley, a tile): at once.
    /// A Powder Keg's blast shakes harder, at once. Plain moves never shake.
    ///
    /// Until its impact the hit checker still stands where it was hit: its real visual is switched off and a stand-in
    /// takes its place, then breaks. Unless it is gone for good, it then comes back where Core sent it (the bar,
    /// mid-board) by putting itself together — the break frames backwards — and only then is the real one shown again.
    /// A checker without break art only shakes; so does one hit while it was the one moving (a Spike Pit), which is
    /// seen going to the bar.
    ///
    /// Purely visual: Core has already moved every checker; this only decides when the screen shows it.
    /// </summary>
    public sealed class HitEffects
    {
        // Safety net: a capturer that never comes to rest still strikes.
        private const float MaxLandingSeconds = 2f;

        /// <summary>A hit checker, from its capture to the end of its break.</summary>
        private sealed class Hit
        {
            public BaseChecker Victim;
            public Sprite[] Frames;

            // What it looked like and where it stood when it was hit. The View may be destroyed by the time it breaks.
            public CheckerController View;
            public Vector3 Position;
            public Sprite Sprite;
            public Color Color;

            // Thrown onto a made point (Maelstrom): the View shows it reach the point; it breaks there.
            public bool BreaksWhereItLands;

            // The stand-in, then the break; the real visual this hit keeps switched off meanwhile.
            public SpriteRenderer Break;
            public CheckerController Hidden;

            // Where the break has got to, and for how long so far: -1 while the stand-in still stands; then the break
            // frames where it was hit, a blank beat (the artist's empty last frame) and the frames backwards where the
            // checker comes back.
            public int Step = -1;
            public float Time;

            public bool Breaks => Frames != null;
        }

        /// <summary>A checker on its way to the point where its hits strike.</summary>
        private sealed class Landing
        {
            public CheckerController Checker;
            public float Waited;
            public readonly List<Hit> Hits = new List<Hit>();
        }

        private readonly CameraShake _shake;
        private readonly ShakeStyle _hitShake;
        private readonly ShakeStyle _blastShake;
        private readonly float _breakFrameTime;
        private readonly Func<BaseChecker, CheckerController> _findView;
        private readonly Func<BaseChecker, Sprite[]> _findBreakFrames;

        // Hits reported since the last tick that no move has claimed: they strike at the tick.
        private readonly List<Hit> _reported = new List<Hit>();

        private readonly List<Landing> _landings = new List<Landing>();

        // Hits with something on screen to look after: a stand-in, a break playing, a real visual switched off.
        private readonly List<Hit> _active = new List<Hit>();

        // Checkers reported crashing (Maelstrom) whose capture is about to be reported.
        private readonly List<BaseChecker> _crashing = new List<BaseChecker>();

        // Visuals of the checkers destroyed this frame, kept from just before BoardManager lets them go: a capture that
        // destroys its victim reports the destruction first.
        private readonly Dictionary<BaseChecker, CheckerController> _destroyed =
            new Dictionary<BaseChecker, CheckerController>();

        private GameState _board;

        /// <param name="shake">The camera's shake; null shakes nothing.</param>
        /// <param name="hitShake">Read at every shake, so edits to it apply at once.</param>
        /// <param name="blastShake">Read at every shake, so edits to it apply at once.</param>
        /// <param name="breakFrameTime">Seconds each break frame shows.</param>
        /// <param name="findView">A checker's visual; null if it has none.</param>
        /// <param name="findBreakFrames">A checker's break frames (its skin's); null or empty = it does not break.</param>
        public HitEffects(CameraShake shake, ShakeStyle hitShake, ShakeStyle blastShake, float breakFrameTime,
                          Func<BaseChecker, CheckerController> findView, Func<BaseChecker, Sprite[]> findBreakFrames)
        {
            _shake = shake;
            _hitShake = hitShake ?? new ShakeStyle();
            _blastShake = blastShake ?? new ShakeStyle();
            _breakFrameTime = Mathf.Max(0.01f, breakFrameTime);
            _findView = findView ?? throw new ArgumentNullException(nameof(findView));
            _findBreakFrames = findBreakFrames ?? throw new ArgumentNullException(nameof(findBreakFrames));
        }

        /// <summary>Off, nothing on the board shakes the screen.</summary>
        public bool ShakeEnabled { get; set; } = true;

        /// <summary>Off, a hit checker goes where Core sent it at once.</summary>
        public bool BreaksEnabled { get; set; } = true;

        /// <summary>How a hit shakes the screen. Edits apply from the next shake.</summary>
        public ShakeStyle HitShake => _hitShake;

        /// <summary>How a Powder Keg's blast shakes the screen. Edits apply from the next shake.</summary>
        public ShakeStyle BlastShake => _blastShake;

        /// <summary>Shakes the screen as <paramref name="style"/> says, whether shaking is on or not: to try it out.</summary>
        public void TryShake(ShakeStyle style)
        {
            if (_shake != null && style != null)
                _shake.Shake(style.Pixels, style.Duration);
        }

        /// <summary>
        /// Follows this match from now on; the previous one is dropped, its breaks cleared away. Attach before the other
        /// listeners of the board: a destroyed checker's visual must be seen before BoardManager lets it go.
        /// </summary>
        public void Attach(GameState board)
        {
            Detach();

            _board = board;
            _board.CheckerDestroyed += HandleCheckerDestroyed;
            _board.CheckerCaptured += HandleCheckerCaptured;
            _board.MoveExecuted += HandleMoveExecuted;
            _board.AbilityTriggered += HandleAbilityTriggered;
        }

        /// <summary>Stops following the match and clears its breaks away. Safe to call more than once.</summary>
        public void Detach()
        {
            if (_board != null)
            {
                _board.CheckerDestroyed -= HandleCheckerDestroyed;
                _board.CheckerCaptured -= HandleCheckerCaptured;
                _board.MoveExecuted -= HandleMoveExecuted;
                _board.AbilityTriggered -= HandleAbilityTriggered;
                _board = null;
            }

            for (int i = 0; i < _active.Count; i++)
                Finish(_active[i]);

            _active.Clear();
            _reported.Clear();
            _landings.Clear();
            _crashing.Clear();
            _destroyed.Clear();
        }

        /// <summary>
        /// Strikes the hits whose moment has come and moves the breaks on by <paramref name="deltaTime"/> seconds.
        /// BoardManager calls it every frame, after the frame's moves and the checkers' glide.
        /// </summary>
        internal void Tick(float deltaTime)
        {
            _crashing.Clear();
            _destroyed.Clear();

            if (_reported.Count > 0)
            {
                Strike(_reported);
                _reported.Clear();
            }

            for (int i = _landings.Count - 1; i >= 0; i--)
            {
                // A checker taken off the board (or out of sight) on its way has nothing left to land: its hits strike now.
                Landing landing = _landings[i];
                landing.Waited += deltaTime;
                if (landing.Checker != null && landing.Checker.gameObject.activeInHierarchy &&
                    !landing.Checker.IsAtTarget && landing.Waited < MaxLandingSeconds)
                    continue;

                _landings.RemoveAt(i);
                Strike(landing.Hits);
            }

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (Advance(_active[i], deltaTime))
                    continue;

                Finish(_active[i]);
                _active.RemoveAt(i);
            }
        }

        private void Shake(ShakeStyle style)
        {
            if (ShakeEnabled && _shake != null)
                _shake.Shake(style.Pixels, style.Duration);
        }

        /// <summary>The moment of impact: the screen shakes once, every hit that breaks starts breaking.</summary>
        private void Strike(List<Hit> hits)
        {
            Shake(_hitShake);

            for (int i = 0; i < hits.Count; i++)
            {
                Hit hit = hits[i];

                if (hit.Breaks && hit.Break == null)
                {
                    bool landed = hit.BreaksWhereItLands && hit.View != null;
                    StandIn(hit, landed ? hit.View.transform.position : hit.Position);
                }

                if (hit.Break == null || hit.Step >= 0)
                    continue;

                hit.Step = 0;
                hit.Time = 0f;
                hit.Break.sprite = hit.Frames[0];
                hit.Break.color = Color.white; // The break art is drawn in its own colours.
            }
        }

        /// <summary>Puts a stand-in where the hit checker stood and switches its real visual off meanwhile.</summary>
        private void StandIn(Hit hit, Vector3 position)
        {
            GameObject go = new GameObject("CheckerBreak");
            go.transform.position = position;

            hit.Break = go.AddComponent<SpriteRenderer>();
            hit.Break.sprite = hit.Sprite;
            hit.Break.color = hit.Color;
            hit.Break.sortingOrder = SortingOrders.CheckerBreak;

            if (hit.View != null)
            {
                // Still switched off by an earlier hit (hit twice in a row): this one takes it over. Off, the visual
                // still takes its new targets from BoardManager; it just does not move, show or take clicks.
                Hit earlier = _active.Find(other => other.Hidden == hit.View);
                if (earlier != null)
                    earlier.Hidden = null;
                else
                    hit.View.gameObject.SetActive(false);

                hit.Hidden = hit.View;
            }

            _active.Add(hit);
        }

        /// <summary>Moves a hit on by one frame's time. False once it is over.</summary>
        private bool Advance(Hit hit, float delta)
        {
            // The stand-in waits for its impact.
            if (hit.Step < 0)
                return true;

            int frames = hit.Frames.Length;

            hit.Time += delta;
            while (hit.Time >= _breakFrameTime)
            {
                hit.Time -= _breakFrameTime;
                hit.Step++;

                if (hit.Step < frames)
                {
                    hit.Break.sprite = hit.Frames[hit.Step];
                }
                else if (hit.Step == frames)
                {
                    // Broken. A checker gone for good stays gone; any other comes back where Core sent it, so the
                    // stand-in goes there for the blank beat and takes the place the real one has in its stack there.
                    if (hit.Hidden == null)
                        return false;

                    hit.Break.sprite = null;
                    hit.Hidden.SnapToTarget();
                    hit.Break.transform.position = hit.Hidden.transform.position;
                    hit.Break.sortingOrder = hit.Hidden.GetComponent<SpriteRenderer>().sortingOrder;
                }
                else if (hit.Step <= 2 * frames)
                {
                    // Putting itself together: the last break frame first, the first one last.
                    hit.Break.sprite = hit.Frames[2 * frames - hit.Step];
                }
                else
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Clears the break away and brings the real checker back, straight onto the place Core sent it.</summary>
        private static void Finish(Hit hit)
        {
            if (hit.Break != null)
            {
                // Edit mode (the tests) cannot wait for the end of the frame.
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(hit.Break.gameObject);
                else
                    UnityEngine.Object.DestroyImmediate(hit.Break.gameObject);

                hit.Break = null;
            }

            if (hit.Hidden != null)
            {
                hit.Hidden.gameObject.SetActive(true);
                hit.Hidden.SnapToTarget();
                hit.Hidden = null;
            }
        }

        private static bool Contains(IReadOnlyList<BaseChecker> checkers, BaseChecker checker)
        {
            for (int i = 0; i < checkers.Count; i++)
            {
                if (checkers[i] == checker)
                    return true;
            }

            return false;
        }

        #region Handlers

        private void HandleCheckerDestroyed(BaseChecker checker, int nodeIndex)
        {
            CheckerController view = _findView(checker);
            if (view != null)
                _destroyed[checker] = view;
        }

        private void HandleCheckerCaptured(BaseChecker checker, int nodeIndex, CaptureDestination destination)
        {
            Hit hit = new Hit { Victim = checker };

            CheckerController view = _findView(checker);
            if (view == null)
                _destroyed.TryGetValue(checker, out view);

            // Taken now: its visual may be gone before the hit strikes (destroyed for good) or already on its way.
            Sprite[] frames = BreaksEnabled ? _findBreakFrames(checker) : null;
            if (view != null && frames != null && frames.Length > 0)
            {
                SpriteRenderer look = view.GetComponent<SpriteRenderer>();
                hit.Frames = frames;
                hit.View = view;
                hit.Position = view.transform.position;
                hit.Sprite = look.sprite;
                hit.Color = look.color;
            }

            if (_crashing.Remove(checker))
            {
                hit.BreaksWhereItLands = true;
                Landing landing = new Landing { Checker = view };
                landing.Hits.Add(hit);
                _landings.Add(landing);
                return;
            }

            _reported.Add(hit);
        }

        private void HandleMoveExecuted(MoveResult result)
        {
            // The moving checker hit on its way (a Spike Pit) is seen going to the bar from where it started.
            for (int i = 0; i < _reported.Count; i++)
            {
                if (_reported[i].Victim == result.MovedChecker)
                    _reported[i].Frames = null;
            }

            if (!result.HasCapture)
                return;

            // The checkers this move hit stand until it lands.
            Landing landing = new Landing { Checker = _findView(result.MovedChecker) };
            for (int i = _reported.Count - 1; i >= 0; i--)
            {
                Hit hit = _reported[i];
                if (!Contains(result.CapturedCheckers, hit.Victim))
                    continue;

                _reported.RemoveAt(i);
                landing.Hits.Add(hit);

                if (hit.Breaks)
                    StandIn(hit, hit.Position);
            }

            _landings.Add(landing);
        }

        private void HandleAbilityTriggered(string abilityId, BaseChecker checker, int nodeIndex)
        {
            if (abilityId == AbilityIds.KegExploded)
                Shake(_blastShake);
            else if (abilityId == AbilityIds.TeleportCrash && checker != null)
                _crashing.Add(checker);
        }

        #endregion
    }
}
