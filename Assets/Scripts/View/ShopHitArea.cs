using System;
using UnityEngine;

namespace Tavla.View
{
    /// <summary>
    /// The clickable box of one shelf, button or sell slot on the shop screen. Only forwards pointer events to
    /// <see cref="ShopView"/>; it knows nothing about what is sold there.
    /// Mouse callbacks work with both input backends, as for <see cref="CheckerController"/>.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    internal sealed class ShopHitArea : MonoBehaviour
    {
        private Action _clicked;
        private Action _entered;
        private Action _exited;

        public void Initialize(Action clicked, Action entered, Action exited)
        {
            _clicked = clicked;
            _entered = entered;
            _exited = exited;
        }

        private void OnMouseDown()
        {
            _clicked?.Invoke();
        }

        private void OnMouseEnter()
        {
            _entered?.Invoke();
        }

        private void OnMouseExit()
        {
            _exited?.Invoke();
        }
    }
}
