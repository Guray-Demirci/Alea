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
    ///   • only says "I want this checker to go to that point" and asks Core.
    /// Whether the move is legal or not, the new position is assigned through Core's events.
    /// That is why no "bounce back" code is needed for illegal moves; it happens by itself.
    ///
    /// Mouse callbacks (OnMouseEnter/Exit/Down/Drag/Up) work with both the legacy Input Manager and the
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
        private bool _isMovable;
        private bool _isHovered;

        // The look of each state, resolved from the skin once in Bind.
        private Sprite _normalSprite;
        private Sprite _movableSprite;
        private Sprite _selectedSprite;
        private Sprite _movableRing;
        private Sprite _selectedRing;

        // Draws the state ring around art that has no state sprites of its own; created only for such a skin.
        private SpriteRenderer _ring;

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
        /// <param name="skin">
        /// The checker's art in each state. Empty sprites keep the sprite the object already has (the white
        /// checker sprite or a prefab's art); empty state sprites fall back to the resting look. State rings
        /// are drawn around the checker on a layer of their own.
        /// </param>
        /// <param name="color">
        /// Tint multiplied into the sprite: a tinted sprite must be WHITE (black x any colour = black);
        /// artwork drawn in its own colours is passed with white.
        /// </param>
        public void Bind(BoardManager board, BaseChecker model, CheckerSkin skin, Color color)
        {
            _board = board;
            _model = model;

            if (_renderer == null)
                _renderer = GetComponent<SpriteRenderer>();

            _normalSprite = skin != null && skin.Normal != null ? skin.Normal : _renderer.sprite;
            _movableSprite = skin != null && skin.Movable != null ? skin.Movable : _normalSprite;
            _selectedSprite = skin != null && skin.Selected != null ? skin.Selected : _normalSprite;
            _movableRing = skin != null ? skin.MovableRing : null;
            _selectedRing = skin != null ? skin.SelectedRing : null;
            _isMovable = false;

            if (_ring == null && (_movableRing != null || _selectedRing != null))
                _ring = CreateRing();

            _renderer.color = color;
            UpdateSprite();

            gameObject.name = $"Checker_{model.TypeId}_P{model.OwnerId}";
        }

        /// <summary>
        /// Whether the checker can be moved with the rolled dice. ONLY BoardManager calls this, with
        /// Core's answer — the checker never works it out itself. The movable look only shows while the
        /// pointer is over the checker.
        /// </summary>
        public void SetMovable(bool movable)
        {
            if (_isMovable == movable)
                return;

            _isMovable = movable;
            UpdateSprite();
        }

        /// <summary>Picked up beats movable-under-the-pointer, which beats resting.</summary>
        private void UpdateSprite()
        {
            bool showMovable = _isMovable && _isHovered;

            _renderer.sprite = _isDragging ? _selectedSprite : showMovable ? _movableSprite : _normalSprite;

            if (_ring != null)
                _ring.sprite = _isDragging ? _selectedRing : showMovable ? _movableRing : null;
        }

        /// <summary>The ring layer: a child drawn in the art's own colours, never tinted.</summary>
        private SpriteRenderer CreateRing()
        {
            GameObject go = new GameObject("Ring");
            go.transform.SetParent(transform, false);

            SpriteRenderer ring = go.AddComponent<SpriteRenderer>();
            ring.sortingOrder = _renderer.sortingOrder;
            return ring;
        }

        /// <summary>The checker and its ring always share a sorting order, so the ring moves with it in a stack.</summary>
        private void SetSortingOrder(int sortingOrder)
        {
            _renderer.sortingOrder = sortingOrder;

            if (_ring != null)
                _ring.sortingOrder = sortingOrder;
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
                SetSortingOrder(sortingOrder);
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

        #region Hover

        private void OnMouseEnter()
        {
            _isHovered = true;
            UpdateSprite();
        }

        /// <summary>Also fires when the checker glides away from a pointer that stays still (after a move).</summary>
        private void OnMouseExit()
        {
            _isHovered = false;
            UpdateSprite();
        }

        #endregion

        #region Drag and drop

        private void OnMouseDown()
        {
            if (_board == null)
                return;

            _isDragging = true;
            UpdateSprite();

            // Keep the offset between the pointer and the checker's centre so it does not jump when grabbed at the edge.
            _grabOffset = transform.position - GetPointerWorldPosition();

            SetSortingOrder(_restingSortingOrder + _dragSortingBoost);

            // Ask CORE where THIS checker may go and highlight that. The View computes no rules,
            // it only paints the list Core returns.
            _board.ShowLegalTargets(_model);
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
            UpdateSprite();
            SetSortingOrder(_restingSortingOrder);

            _board.ClearHighlights();

            NodeView target = FindNodeUnderPointer();

            if (target == null)
            {
                // Dropped outside the board: we do not ask Core at all, it is not even a move attempt.
                // The position is still refreshed from Core's state so no hand-moved visual is left behind.
                _board.RefreshBoard();
                return;
            }

            // Core is told which checker was dropped; it looks up where that checker is itself.
            _board.RequestMove(_model, target.NodeIndex);

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
