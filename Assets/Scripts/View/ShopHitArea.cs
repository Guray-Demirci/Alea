using UnityEngine;

namespace Tavla.View
{
    /// <summary>
    /// The clickable box of one shelf or button on the shop screen. Only forwards pointer events to
    /// <see cref="ShopView"/>; it knows nothing about what is sold there.
    /// Mouse callbacks work with both input backends, as for <see cref="CheckerController"/>.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    internal sealed class ShopHitArea : MonoBehaviour
    {
        private ShopView _view;
        private ShopTarget _target;

        public void Initialize(ShopView view, ShopTarget target)
        {
            _view = view;
            _target = target;
        }

        private void OnMouseDown()
        {
            if (_view != null)
                _view.HandleClick(_target);
        }

        private void OnMouseEnter()
        {
            if (_view != null)
                _view.HandlePointerEnter(_target);
        }

        private void OnMouseExit()
        {
            if (_view != null)
                _view.HandlePointerExit(_target);
        }
    }
}
