using System.Threading;
using System.Threading.Tasks;
using Drakken.Client.World.Vfx;
using UnityEngine;

namespace Drakken.Client
{
    // Home for simple, hardcoded VFX that tokens can trigger as part of their animations,
    // e.g. Bolster's "+1" popup. Add new effects here as methods, alongside whatever
    // prefab/asset references they need, rather than scattering ad-hoc VFX code per-token.
    public class ClientVfx : MonoBehaviour
    {
        private GameClient client;

        public void Init(GameClient client)
        {
            this.client = client;
        }

        public Task SpawnFloatingLabel(
            string text, Color color, Vector3 worldPosition, Quaternion rotation, CancellationToken ct)
            => FloatingLabel.Spawn(client.Assets, text, color, worldPosition, rotation, ct);
    }
}
