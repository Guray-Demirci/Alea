using UnityEngine;
using Tavla.Core.Checkers;

namespace Tavla.View
{
    /// <summary>
    /// Visual representative of a single checker in the scene + drag-and-drop input.
    ///
    /// DUMB PUPPET CONTRACT — this script:
    ///   • never computes distances, never checks point occupancy, never checks whose turn it is,
    ///   • NEVER decides its own position when dropped,
    ///   • only says "I want to go from this point to that point" and asks Core.
    /// Whether the move is legal or not, the new position is assigned through Core's events.
    /// That is why no "bounce back" code is needed for illegal moves; it happens by itself.
    ///
    /// Mouse callbacks (OnMouseDown/Drag/Up) work with both the legacy Input Manager and the
    /// Input System package on Unity 6.4+; the pointer position is read through <see cref="InputAdapter"/>.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(BoxCollider2D))]
    public class CheckerController : MonoBehaviour
    {
        [Header("Movement")]
        [Tooltip("Speed at which the checker glides to its target (units/second).")]
        [SerializeField] private float _moveSpeed = 14f;

        [Header("Dragging")]
        [Tooltip("Added to the sorting order while dragging so the checker is drawn above the others.")]
        [SerializeField] private int _dragSortingBoost = SortingOrders.DragBoost;

        private BoardManager _board;
        private BaseChecker _model;
        private SpriteRenderer _renderer;
        private Camera _camera;

        private Vector3 _targetPosition;
        private Vector3 _grabOffset;
        private int _restingSortingOrder;
        private bool _isDragging;

        // Reused for the drop query so releasing a checker does not allocate. Sized for the worst case:
        // all 30 checkers stacked under the pointer plus the point collider — a smaller buffer could
        // truncate the results and silently drop the NodeView we are looking for.
        private static readonly Collider2D[] DropHits = new Collider2D[32];

        /// <summary>The Core object this visual represents. The View uses it ONLY as an identity.</summary>
        public BaseChecker Model => _model;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _camera = Camera.main;
        }

        /// <summary>Called by BoardManager when it spawns the checker.</summary>
        public void Bind(BoardManager board, BaseChecker model, Color color)
        {
            _board = board;
            _model = model;

            if (_renderer == null)
                _renderer = GetComponent<SpriteRenderer>();

            // NOTE: the source sprite must be WHITE. checker_scheme.aseprite is a black silhouette, so
            // tinting does not work with it (black x any colour = black).
            _renderer.color = color;

            gameObject.name = $"Checker_{model.TypeId}_P{model.OwnerId}";
        }

        /// <summary>
        /// New resting position. ONLY BoardManager calls this, when Core raises an event.
        /// Calls during a drag update the target but do not interrupt the visual following the pointer.
        /// </summary>
        public void SetTarget(Vector3 position, int sortingOrder)
        {
            _targetPosition = position;
            _restingSortingOrder = sortingOrder;

            if (!_isDragging)
                _renderer.sortingOrder = sortingOrder;
        }

        /// <summary>Instant placement without animation (for the initial setup).</summary>
        public void SnapToTarget()
        {
            transform.position = _targetPosition;
        }

        private void Update()
        {
            // While dragging the pointer is in charge; at any other time we glide towards the target.
            if (_isDragging)
                return;

            transform.position = Vector3.MoveTowards(
                transform.position, _targetPosition, _moveSpeed * Time.deltaTime);
        }

        #region Drag and drop

        private void OnMouseDown()
        {
            if (_board == null)
                return;

            _isDragging = true;

            // Keep the offset between the pointer and the checker's centre so it does not jump when grabbed at the edge.
            _grabOffset = transform.position - GetPointerWorldPosition();

            _renderer.sortingOrder = _restingSortingOrder + _dragSortingBoost;

            // Ask CORE where it may go and highlight that. The View computes no rules,
            // it only paints the list Core returns.
            _board.ShowLegalTargets(_board.GetSourceIndex(_model));
        }

        private void OnMouseDrag()
        {
            if (!_isDragging)
                return;

            transform.position = GetPointerWorldPosition() + _grabOffset;
        }

        private void OnMouseUp()
        {
            if (!_isDragging)
                return;

            _isDragging = false;
            _renderer.sortingOrder = _restingSortingOrder;

            _board.ClearHighlights();

            NodeView target = FindNodeUnderPointer();

            if (target == null)
            {
                // Dropped outside the board: we do not ask Core at all, it is not even a move attempt.
                // The position is still refreshed from Core's state so no hand-moved visual is left behind.
                _board.RefreshBoard();
                return;
            }

            // The source index also comes from the MODEL — the checker does not know where it is.
            int fromIndex = _board.GetSourceIndex(_model);

            _board.RequestMove(fromIndex, target.NodeIndex);

            // Nothing else happens here. Accepted or rejected, the new position arrives
            // through the MoveExecuted / MoveRejected events.
        }

        private Vector3 GetPointerWorldPosition()
        {
            if (_camera == null)
                _camera = Camera.main;

            Vector3 screen = InputAdapter.PointerScreenPosition;
            screen.z = -_camera.transform.position.z; // Project onto the board plane (orthographic camera).

            Vector3 world = _camera.ScreenToWorldPoint(screen);
            world.z = 0f;
            return world;
        }

        /// <summary>The point under the pointer. Null if there is none.</summary>
        private NodeView FindNodeUnderPointer()
        {
            Vector3 world = GetPointerWorldPosition();

            ContactFilter2D filter = new ContactFilter2D();
            filter.useTriggers = true; // Point colliders are triggers.

            int hitCount = Physics2D.OverlapPoint(world, filter, DropHits);

            for (int i = 0; i < hitCount; i++)
            {
                NodeView node = DropHits[i].GetComponent<NodeView>();
                if (node != null)
                    return node;
            }

            return null;
        }

        #endregion
    }
}
