using UnityEngine;

namespace Tavla.View
{
    /// <summary>
    /// Shakes the camera it sits on, the pixel-art way. It moves in whole canvas pixels only, so the art stays
    /// pixel-exact; it picks a new offset every <see cref="_stepInterval"/> seconds rather than every frame, so it looks
    /// the same at any frame rate; and it fades out instead of stopping dead.
    ///
    /// Shakes do not stack: a stronger one takes over, a weaker one during it is dropped, and the camera always goes
    /// back to where it rested — two shakes at once can never leave it off-centre. <see cref="HitEffects"/> decides
    /// when it shakes; <see cref="BoardSceneBuilder.AddCameraShake"/> puts it on the camera.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraShake : MonoBehaviour
    {
        [Tooltip("Pixels of the board art per world unit (the BoardLayout's PPU), so offsets land on whole pixels.")]
        [SerializeField, Min(1f)] private float _pixelsPerUnit = 12f;
        [Tooltip("Seconds between two offsets. The dice cards turn at 0.07 s a frame.")]
        [SerializeField, Min(0.01f)] private float _stepInterval = 0.04f;

        private Vector3 _restPosition;
        private bool _shaking;

        // Strength in pixels; it fades to zero at _fadeRate pixels a second.
        private float _power;
        private float _fadeRate;

        private float _untilNextStep;

        // The horizontal offset swings from side to side, so every step visibly moves.
        private int _side = 1;

        public bool IsShaking => _shaking;

        /// <summary>Pixels of the board art per world unit, so offsets land on whole pixels.</summary>
        public float PixelsPerUnit
        {
            get => _pixelsPerUnit;
            set => _pixelsPerUnit = Mathf.Max(1f, value);
        }

        /// <summary>Shakes up to <paramref name="pixels"/> canvas pixels, fading out over <paramref name="duration"/> seconds.</summary>
        public void Shake(int pixels, float duration)
        {
            if (pixels <= 0 || duration <= 0f)
                return;

            // A weaker shake inside a stronger one would add nothing anyone can see.
            if (_shaking && pixels < _power)
                return;

            if (!_shaking)
            {
                _restPosition = transform.localPosition;
                _shaking = true;
            }

            _power = pixels;
            _fadeRate = pixels / duration;
            _untilNextStep = 0f;
        }

        /// <summary>Ends the shake at once and puts the camera back where it rested.</summary>
        public void Stop()
        {
            if (!_shaking)
                return;

            _shaking = false;
            transform.localPosition = _restPosition;
        }

        private void LateUpdate()
        {
            if (!_shaking)
                return;

            float delta = Time.unscaledDeltaTime;

            _power -= _fadeRate * delta;
            if (_power <= 0f)
            {
                Stop();
                return;
            }

            _untilNextStep -= delta;
            if (_untilNextStep > 0f)
                return;

            _untilNextStep = _stepInterval;

            int reach = Mathf.Max(1, Mathf.RoundToInt(_power));
            _side = -_side;
            int x = _side * Random.Range(1, reach + 1);
            int y = Random.Range(-reach, reach + 1);

            float unit = 1f / _pixelsPerUnit;
            transform.localPosition = _restPosition + new Vector3(x * unit, y * unit, 0f);
        }

        private void OnDisable()
        {
            Stop();
        }
    }
}
