using UnityEngine;

namespace Drakken.Client.World
{
    public class CameraController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera cam;

        [Header("Config")]
        [SerializeField] private float mousePanAmount = 3.5f;
        [SerializeField] private float rotationLerpSpeed = 5f;

        private Quaternion baseRotation;

        void Awake()
        {
            baseRotation = transform.rotation;
        }

        private void Update()
        {
            Quaternion pannedRotation = GetMousePannedRotation(baseRotation);

            cam.transform.rotation = Quaternion.Lerp(
                cam.transform.rotation,
                pannedRotation,
                rotationLerpSpeed * Time.deltaTime);
        }

        private Quaternion GetMousePannedRotation(Quaternion rot)
        {
            float xCurrent = (rot.eulerAngles.x < 180f) ? rot.eulerAngles.x : (rot.eulerAngles.x - 360f);
            float yCurrent = (rot.eulerAngles.y < 180f) ? rot.eulerAngles.y : (rot.eulerAngles.y - 360f);

            float xPct = Mathf.Min(Mathf.Max(2f * (Input.mousePosition.y / Screen.height - 0.5f), -1f), 1f);
            float yPct = Mathf.Min(Mathf.Max(2f * (Input.mousePosition.x / Screen.width - 0.5f), -1f), 1f);

            float xPanned = xCurrent - mousePanAmount * xPct;
            float yPanned = yCurrent + mousePanAmount * yPct;

            Quaternion target = Quaternion.Euler(xPanned, yPanned, rot.eulerAngles.z);

            return target;
        }
    }
}
