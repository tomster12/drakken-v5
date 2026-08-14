using System.Threading;
using System.Threading.Tasks;
using Drakken.Domain.Animation;
using Drakken.Utility;
using TMPro;
using UnityEngine;

namespace Drakken.Client.World.Vfx
{
    public static class FloatingLabel
    {
        public static async Task Spawn(
            AssetDatabase assets,
            string text,
            Color color,
            Vector3 worldPosition,
            Quaternion rotation,
            CancellationToken ct,
            float durationSeconds = 0.9f,
            float riseDistance = 0.4f,
            float fontSize = 3f)
        {
            GameObject labelGo = new($"Floating Label ({text})");
            var label = labelGo.AddComponent<TextMeshPro>();

            label.rectTransform.SetPositionAndRotation(worldPosition, rotation);
            label.text = text;
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = fontSize;
            label.color = color;

            if (assets.DiceFaceLabelFont != null) label.font = assets.DiceFaceLabelFont;
            if (assets.DiceFaceLabelMaterial != null) label.material = assets.DiceFaceLabelMaterial;

            Vector3 startPosition = worldPosition;
            Vector3 endPosition = worldPosition + Vector3.up * riseDistance;

            var player = new AnimationPlayer();

            try
            {
                // Move upwards and fade alpha over time
                await player.Play(AnimationSequenceBuilder.Start()
                    .Next(
                        AnimationTracks.PositionFunc(
                            durationSeconds, label.transform,
                            t => Vector3.Lerp(startPosition, endPosition, t), Easing.EaseOutCubic),

                        new AnimationTrack<float>(
                            durationSeconds,
                            t => Mathf.Lerp(1f, 0f, Easing.EaseInCubic(t)),
                            alpha =>
                            {
                                var c = label.color;
                                c.a = alpha;
                                label.color = c;
                            }))
                    .Build(), ct);
            }
            finally
            {
                if (labelGo != null) GameObject.Destroy(labelGo);
            }
        }
    }
}
