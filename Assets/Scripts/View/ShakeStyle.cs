using System;
using UnityEngine;

namespace Tavla.View
{
    /// <summary>How hard and how long one kind of moment shakes the screen (<see cref="HitEffects"/>).</summary>
    [Serializable]
    public sealed class ShakeStyle
    {
        public const int MaxPixels = 4;

        [Tooltip("Furthest the camera moves, in canvas pixels.")]
        [Range(1, MaxPixels)] public int Pixels = 1;

        [Tooltip("Seconds until the shake has died down.")]
        [Range(0.05f, 0.6f)] public float Duration = 0.15f;
    }
}
