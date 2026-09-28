using UnityEngine;

namespace Tavla.View
{
    /// <summary>
    /// VISUAL geometry of the board. Values were measured pixel by pixel from play_game_scheme.aseprite.
    /// There are no game rules here — only the answer to "where is point 7 on screen?".
    ///
    /// Why a ScriptableObject? So that when the artist changes the triangle spacing nothing has to be
    /// recompiled; only the asset is updated. If none is assigned, the owner falls back to the defaults
    /// via <see cref="CreateDefault"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "BoardLayout", menuName = "Tavla/Board Layout")]
    public class BoardLayout : ScriptableObject
    {
        [Header("Canvas (Aseprite scheme)")]
        [SerializeField] private int _canvasWidth = 320;
        [SerializeField] private int _canvasHeight = 180;
        [SerializeField] private float _pixelsPerUnit = 12f;

        [Header("Triangle layout (pixels)")]
        [Tooltip("Left edge of the first triangle.")]
        [SerializeField] private int _firstTriangleX = 90;
        [Tooltip("Distance between two triangles (12 width + 2 gap).")]
        [SerializeField] private int _trianglePitch = 14;
        [SerializeField] private int _triangleWidth = 12;
        [Tooltip("Bar offset added from the 6th triangle on.")]
        [SerializeField] private int _barOffset = 14;
        [SerializeField] private int _triangleHeight = 56;

        [Tooltip("Top edge of the top-row triangles.")]
        [SerializeField] private int _topRowTopY = 18;
        [Tooltip("Bottom edge of the bottom-row triangles (162 because 161 is inclusive).")]
        [SerializeField] private int _bottomRowBottomY = 162;

        [Header("Checker stacking")]
        [SerializeField] private int _checkerSize = 12;
        [Tooltip("Spacing between stacked checkers. 11 px -> 5 checkers take 55 px and fit a 56 px triangle.")]
        [SerializeField] private int _stackStep = 11;

        [Tooltip("Keeps checkers IN FRONT of the points on the z axis. Otherwise mouse clicks hit the " +
                 "point collider and dragging never starts.")]
        [SerializeField] private float _checkerZ = -1f;

        [Header("Bar (hit checkers)")]
        [SerializeField] private int _barCenterX = 180;
        [SerializeField] private int _barCenterY = 90;
        [SerializeField] private int _barStackStep = 4;
        [Tooltip("Height of the bar gap per player (half of the 32 px gap).")]
        [SerializeField] private int _barHalfHeight = 16;

        [Header("Bear-off area")]
        [Tooltip("Horizontal centre of the empty strip right of the board. The frame ends at 279 px; " +
                 "the left side belongs to the info panel.")]
        [SerializeField] private int _bearOffCenterX = 298;
        [SerializeField] private int _bearOffWidth = 28;

        /// <summary>Number of columns per row (6 points on each side of the bar).</summary>
        private const int ColumnsPerRow = 12;
        private const int ColumnsPerQuadrant = 6;

        /// <summary>
        /// Creates an in-memory layout with the default values. The caller owns the instance and
        /// must destroy it when done (it is never saved to disk).
        /// </summary>
        public static BoardLayout CreateDefault()
        {
            BoardLayout layout = CreateInstance<BoardLayout>();
            layout.name = "BoardLayout (Default)";
            layout.hideFlags = HideFlags.DontSave;
            return layout;
        }

        public float PixelsPerUnit => _pixelsPerUnit;

        /// <summary>Orthographic camera size for a pixel-exact ratio.</summary>
        public float CameraOrthographicSize => (_canvasHeight * 0.5f) / _pixelsPerUnit;

        /// <summary>Checker size in world units (for the collider).</summary>
        public float CheckerWorldSize => _checkerSize / _pixelsPerUnit;

        /// <summary>
        /// Converts an Aseprite pixel coordinate into a Unity world coordinate.
        /// In Aseprite y grows DOWNWARD, in Unity UPWARD — the conversion happens here, in one place.
        /// The centre of the board is the world origin (0,0).
        /// </summary>
        public Vector3 PixelToWorld(float px, float py)
        {
            float x = (px - _canvasWidth * 0.5f) / _pixelsPerUnit;
            float y = (_canvasHeight * 0.5f - py) / _pixelsPerUnit;
            return new Vector3(x, y, 0f);
        }

        /// <summary>
        /// POINT INDEX -> SCREEN MAPPING (the contract of the architecture):
        ///   0..11  -> top row, LEFT TO RIGHT
        ///   12..23 -> bottom row, RIGHT TO LEFT
        /// So player 0 (0 -> 23) starts top-left and draws a U down to the bottom-left.
        /// </summary>
        public bool IsTopRow(int nodeIndex) => nodeIndex < ColumnsPerRow;

        /// <summary>Screen column (0-11, left to right) of a point index.</summary>
        public int GetColumn(int nodeIndex) =>
            nodeIndex < ColumnsPerRow ? nodeIndex : (2 * ColumnsPerRow - 1) - nodeIndex;

        /// <summary>Horizontal centre of a column in pixels. The bar gap is built into the formula.</summary>
        private float GetColumnCenterX(int column)
        {
            int left = _firstTriangleX + column * _trianglePitch + (column >= ColumnsPerQuadrant ? _barOffset : 0);
            return left + _triangleWidth * 0.5f;
        }

        /// <summary>Centre of the triangle — position and collider of the NodeView.</summary>
        public Vector3 GetNodeCenter(int nodeIndex)
        {
            float px = GetColumnCenterX(GetColumn(nodeIndex));

            float py = IsTopRow(nodeIndex)
                ? _topRowTopY + _triangleHeight * 0.5f
                : _bottomRowBottomY - _triangleHeight * 0.5f;

            return PixelToWorld(px, py);
        }

        /// <summary>Size of the NodeView collider in world units.</summary>
        public Vector2 GetNodeSize()
        {
            return new Vector2(_triangleWidth / _pixelsPerUnit, _triangleHeight / _pixelsPerUnit);
        }

        /// <summary>
        /// Centre of the bear-off drop area. It sits OUTSIDE the board: on top of the triangles its
        /// collider would steal checker clicks. Vertically it spans both triangle rows — one area is
        /// enough for both players, Core knows who is bearing off from the current player.
        /// </summary>
        public Vector3 GetBearOffCenter()
        {
            return PixelToWorld(_bearOffCenterX, (_topRowTopY + _bottomRowBottomY) * 0.5f);
        }

        /// <summary>Size of the bear-off collider in world units.</summary>
        public Vector2 GetBearOffSize()
        {
            return new Vector2(_bearOffWidth / _pixelsPerUnit,
                               (_bottomRowBottomY - _topRowTopY) / _pixelsPerUnit);
        }

        /// <summary>
        /// Fits the stack spacing into the available space.
        ///
        /// Core allows all 15 checkers on a single point. With a fixed 11 px step, 15 checkers take
        /// 154 px — far beyond a 56 px triangle. This calculation overlaps checkers as the stack grows
        /// so it always stays inside the area. For small stacks the preferred step is kept as is.
        /// </summary>
        private static float FitStep(float preferredStep, float usableHeight, int stackCount)
        {
            if (stackCount <= 1 || usableHeight <= 0f)
                return preferredStep;

            // There are stackCount-1 gaps (the first checker itself is not counted).
            float fitted = usableHeight / (stackCount - 1);
            return fitted < preferredStep ? fitted : preferredStep;
        }

        /// <summary>
        /// Position of the k-th checker on a point.
        /// On the top row checkers stack downward, on the bottom row upward — inward from the triangle's base.
        /// </summary>
        /// <param name="stackCount">Total checkers on the point; the spacing shrinks accordingly.</param>
        public Vector3 GetCheckerPosition(int nodeIndex, int stackIndex, int stackCount)
        {
            float px = GetColumnCenterX(GetColumn(nodeIndex));

            // The checker size is subtracted so the CENTRE of the last checker stays inside the triangle.
            float step = FitStep(_stackStep, _triangleHeight - _checkerSize, stackCount);

            float py = IsTopRow(nodeIndex)
                ? _topRowTopY + stackIndex * step + _checkerSize * 0.5f
                : _bottomRowBottomY - stackIndex * step - _checkerSize * 0.5f;

            Vector3 world = PixelToWorld(px, py);
            world.z = _checkerZ;
            return world;
        }

        /// <summary>Position of the k-th checker on the bar. Player 0 stacks up from the centre, player 1 down.</summary>
        /// <param name="stackCount">Total checkers on the bar; the spacing shrinks accordingly.</param>
        public Vector3 GetBarPosition(int playerId, int stackIndex, int stackCount)
        {
            float direction = playerId == 0 ? -1f : 1f;
            float step = FitStep(_barStackStep, _barHalfHeight - _checkerSize, stackCount);

            float py = _barCenterY + direction * (_checkerSize * 0.5f + stackIndex * step);

            Vector3 world = PixelToWorld(_barCenterX, py);
            world.z = _checkerZ;
            return world;
        }
    }
}
