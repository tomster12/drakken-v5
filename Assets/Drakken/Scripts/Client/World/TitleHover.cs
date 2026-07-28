using UnityEngine;

namespace Drakken.Client.World
{
    public class TitleHover : MonoBehaviour
    {

        [Header("Config")]
        [SerializeField] private float floatDuration = 1.65f;
        [SerializeField] private float floatMagnitude = 0.1f;

        private float timeOffset;
        private float initialY;

        private void Awake()
        {
            // Initialize variables
            timeOffset = Time.time;
            initialY = transform.position.y;
        }

        private void Update()
        {
            // Update y position based on time
            float time = (Time.time - timeOffset);
            float newY = initialY + Mathf.Sin(time / floatDuration * Mathf.PI * 2f) * floatMagnitude;
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);
        }
    }
}
