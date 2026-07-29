using UnityEngine;

namespace Drakken.Client.World
{
    public class CameraController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera cam;

        [Header("Config")]
        [SerializeField] private float mousePanAmount = 1f;
        [SerializeField] private float panLerpSpeed = 2f;
        [SerializeField] private float rotationLerpSpeed = 4f;
        [SerializeField] private float movementLerpSpeed = 4f;

        private Vector3 targetPosition;
        private Quaternion targetRotation;
        private Quaternion currentPanRotation;

        void Awake()
        {
            targetPosition = transform.position;
            targetRotation = transform.rotation;
        }

        private void Update()
        {
            // Lerp pan rotation
            Quaternion targetPanRotation = GetMousePanRotation();

            currentPanRotation = Quaternion.Lerp(
                currentPanRotation,
                targetPanRotation,
                panLerpSpeed * Time.deltaTime);

            // Lerp towards target
            transform.position = Vector3.Lerp(
                transform.position,
                targetPosition,
                movementLerpSpeed * Time.deltaTime);

            transform.rotation = Quaternion.Lerp(
                transform.rotation,
                currentPanRotation * targetRotation,
                rotationLerpSpeed * Time.deltaTime);
        }

        private Quaternion GetMousePanRotation()
        {
            float xPct = Mathf.Min(Mathf.Max(2f * (Input.mousePosition.y / Screen.height - 0.5f), -1f), 1f);
            float yPct = Mathf.Min(Mathf.Max(2f * (Input.mousePosition.x / Screen.width - 0.5f), -1f), 1f);

            float xPanned = -mousePanAmount * xPct;
            float yPanned = mousePanAmount * yPct;

            return Quaternion.Euler(xPanned, yPanned, 0);
        }

        public void SetTarget(Transform transform)
        {
            targetPosition = transform.position;
            targetRotation = transform.rotation;
        }
    }
}
