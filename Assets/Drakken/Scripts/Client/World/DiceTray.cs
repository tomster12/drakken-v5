using UnityEngine;

namespace Drakken.Client.World
{
    // Root object for a dice tray: place floor/wall colliders as children of this transform in the
    // editor. DiceSimulationWorld clones the whole hierarchy into each match's local physics scene.
    public class DiceTray : MonoBehaviour
    {
        public Vector3 Size = new(4f, 0f, 4f);
    }
}
