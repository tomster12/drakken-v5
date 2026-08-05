using System;
using UnityEngine;
using Drakken.Utility;

namespace Drakken.Client.World.Animation
{
    public static class AnimationTracks
    {
        public static IAnimationTrack Position(
            float durationSeconds, Transform transform, Vector3 startPosition, Vector3 endPosition, Func<float, float> easingFunc = null)
        {
            easingFunc ??= Easing.Linear;
            return new AnimationTrack<Vector3>(
                durationSeconds,
                normalisedTime => Vector3.Lerp(startPosition, endPosition, easingFunc(normalisedTime)),
                position => transform.position = position);
        }

        public static IAnimationTrack PositionFunc(
            float durationSeconds, Transform transform, Func<float, Vector3> getPosition, Func<float, float> easingFunc = null)
        {
            easingFunc ??= Easing.Linear;
            return new AnimationTrack<Vector3>(
                durationSeconds,
                normalisedTime => getPosition(easingFunc(normalisedTime)),
                position => transform.position = position);
        }

        public static IAnimationTrack Rotation(
            float durationSeconds, Transform transform, Quaternion startRotation, Quaternion endRotation, Func<float, float> easingFunc = null)
        {
            easingFunc ??= Easing.Linear;
            return new AnimationTrack<Quaternion>(
                durationSeconds,
                normalisedTime => Quaternion.Slerp(startRotation, endRotation, easingFunc(normalisedTime)),
                rotation => transform.rotation = rotation);
        }

        public static IAnimationTrack EulerRotation(
            float durationSeconds, Transform transform, Quaternion startRotation, Vector3 amount, Func<float, float> easingFunc = null)
        {
            easingFunc ??= Easing.Linear;
            return new AnimationTrack<Quaternion>(
                durationSeconds,
                normalisedTime => Quaternion.Euler(
                    Vector3.Lerp(startRotation.eulerAngles, startRotation.eulerAngles + amount, easingFunc(normalisedTime))),
                rotation => transform.rotation = rotation);
        }

        public static IAnimationTrack LocalScale(
            float durationSeconds, Transform transform, Vector3 startLocalScale, Vector3 endLocalScale, Func<float, float> easingFunc = null)
        {
            easingFunc ??= Easing.Linear;
            return new AnimationTrack<Vector3>(
                durationSeconds,
                normalisedTime => Vector3.Lerp(startLocalScale, endLocalScale, easingFunc(normalisedTime)),
                localScale => transform.localScale = localScale);
        }
    }
}
