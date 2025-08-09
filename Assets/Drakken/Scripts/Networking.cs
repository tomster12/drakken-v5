using Drakken.Common;
using System.Threading.Tasks;
using Unity.Netcode;

namespace Drakken
{
    public class Networking : NetworkBehaviour
    {
        public static Networking Singleton { get; private set; }

        private TaskManager tasks = new();

        private void Awake()
        {
            Singleton = this;
        }

        // ----------------------------------------------

        public Task<bool> RequestJoinGameAsync()
        {
            var (taskId, task) = tasks.Create<bool>();
            RequestJoinServerRpc(taskId);
            return task;
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestJoinServerRpc(ulong taskId, ServerRpcParams rpcParams = default)
        {
            var senderId = rpcParams.Receive.SenderClientId;
            var response = Server.Singleton.OnRequestJoinGame(senderId);
            RequestJoinGameResponseClientRpc(taskId, response, MakeClientRpcParams(senderId));
        }

        [ClientRpc]
        private void RequestJoinGameResponseClientRpc(ulong taskId, bool response, ClientRpcParams rpcParams = default)
        {
            tasks.Complete(taskId, response);
        }

        // ----------------------------------------------

        private static ClientRpcParams MakeClientRpcParams(ulong id)
        {
            return new() { Send = new() { TargetClientIds = new[] { id } } };
        }
    }
}
