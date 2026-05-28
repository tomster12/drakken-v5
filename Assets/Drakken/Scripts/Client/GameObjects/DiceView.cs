using Drakken.Domain;
using TMPro;
using UnityEngine;

namespace Drakken.Client.GameObjects
{
    public class DiceView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private new Renderer renderer;
        [SerializeField] private TextMeshPro labelValue;
        [SerializeField] private TextMeshPro labelSides;

        [Header("Config")]
        [SerializeField] private Color colorNormal = new(0.19f, 0.15f, 0.133f);
        [SerializeField] private Color colorAffected = new Color(1f, 0.6f, 0.2f);

        private DiceInstance dice;

        public void Bind(DiceInstance dice)
        {
            this.dice = dice;
            Refresh();
        }

        public void Refresh()
        {
            if (dice == null) return;

            if (labelValue != null) labelValue.text = dice.Value.ToString();
            if (labelSides != null) labelSides.text = $"D{dice.Sides}";

            bool hasEffects = dice.Effects != null && dice.Effects.Count > 0;
            if (renderer != null)
                renderer.material.color = hasEffects ? colorAffected : colorNormal;
        }
    }
}