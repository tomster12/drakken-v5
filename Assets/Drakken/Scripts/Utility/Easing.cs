using System;
using UnityEngine;

namespace Drakken.Utility
{
    public static class Easing
    {
        public static float Linear(float t) => t;

        public static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);

        public static float EaseInCubic(float t) => Mathf.Pow(t, 3f);

        public static float EaseInOutCubic(float t) =>
            t < 0.5f ? 4f * t * t * t
                     : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
    }
}