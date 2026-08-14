using System.Threading;
using System.Threading.Tasks;
using Drakken.Client.World.Vfx;
using UnityEngine;

namespace Drakken.Client
{
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
