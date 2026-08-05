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

        public static float EaseInOutQuad(float t) =>
            t < 0.5f ? 2f * t * t
                     : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;

        public static float EaseInSin(float t) => 1f - Mathf.Cos((t * Mathf.PI) / 2f);

        public static float InverseEaseInOutSin(float x)
        {
            if (x < 0.5f) return Mathf.Asin(x * 2f) / Mathf.PI;
            return 1f - Mathf.Asin((1f - x) * 2f) / Mathf.PI;
        }

        public static float InverseEaseInOutQuad(float x)
        {
            if (x < 0.5f) return Mathf.Sqrt(x * 0.5f);
            return 1f - Mathf.Sqrt((1f - x) * 0.5f);
        }

        public static float InverseEaseInOutCubic(float x)
        {
            if (x < 0.5f) return Mathf.Pow(x * 2f, 1f / 3f) * 0.5f;
            return 1f - Mathf.Pow((1f - x) * 2f, 1f / 3f) * 0.5f;
        }
    }
}
