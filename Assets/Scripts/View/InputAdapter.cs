#if ENABLE_INPUT_SYSTEM && TAVLA_INPUT_SYSTEM_PACKAGE
#define TAVLA_NEW_INPUT
#endif

using UnityEngine;
#if TAVLA_NEW_INPUT
using UnityEngine.InputSystem;
#endif

namespace Tavla.View
{
    /// <summary>
    /// The single place that reads pointer data from Unity's input backends.
    ///
    /// Uses the Input System package when it is installed AND enabled in Player Settings
    /// (Active Input Handling = "Input System Package" or "Both"); otherwise falls back to the
    /// legacy Input Manager. Either way the rest of the View stays backend-agnostic.
    ///
    /// <c>TAVLA_INPUT_SYSTEM_PACKAGE</c> is defined by Tavla.View.asmdef (versionDefines) only when
    /// the package is present, so the project compiles with or without it.
    ///
    /// Note: MonoBehaviour.OnMouseDown/Drag/Up are delivered by both backends on Unity 6.4+,
    /// so drag-and-drop on checkers needs no backend-specific code.
    /// </summary>
    internal static class InputAdapter
    {
        /// <summary>Current pointer (mouse, pen or primary touch) position in screen pixels.</summary>
        public static Vector2 PointerScreenPosition
        {
            get
            {
#if TAVLA_NEW_INPUT
                Pointer pointer = Pointer.current;
                return pointer != null ? pointer.position.ReadValue() : Vector2.zero;
#else
                return Input.mousePosition;
#endif
            }
        }
    }
}
