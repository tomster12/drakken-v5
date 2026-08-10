using System;
using UnityEngine;

namespace Drakken.Domain.Animation
{
    public static class AnimationCurves
    {
        public static Func<float, Vector3> Lerp(Vector3 startLocalPosition, Vector3 endLocalPosition)
        {
            return normalizedTime => Vector3.Lerp(startLocalPosition, endLocalPosition, normalizedTime);
        }

        public static Func<float, Vector3> QuadraticBezier(Vector3 startLocalPosition, Vector3 controlLocalPosition, Vector3 endLocalPosition)
        {
            return normalizedTime =>
            {
                Vector3 startToControlLocalPosition = Vector3.Lerp(startLocalPosition, controlLocalPosition, normalizedTime);
                Vector3 controlToEndLocalPosition = Vector3.Lerp(controlLocalPosition, endLocalPosition, normalizedTime);
                return Vector3.Lerp(startToControlLocalPosition, controlToEndLocalPosition, normalizedTime);
            };
        }

        public static Func<float, Vector3> CubicBezier(Vector3 startLocalPosition, Vector3 controlOneLocalPosition, Vector3 controlTwoLocalPosition, Vector3 endLocalPosition)
        {
            return normalizedTime =>
            {
                float inverseNormalizedTime = 1f - normalizedTime;

                Vector3 term1 = inverseNormalizedTime * inverseNormalizedTime * inverseNormalizedTime * startLocalPosition;
                Vector3 term2 = 3f * inverseNormalizedTime * inverseNormalizedTime * normalizedTime * controlOneLocalPosition;
                Vector3 term3 = 3f * inverseNormalizedTime * normalizedTime * normalizedTime * controlTwoLocalPosition;
                Vector3 term4 = normalizedTime * normalizedTime * normalizedTime * endLocalPosition;

                return term1 + term2 + term3 + term4;
            };
        }
    }
}
