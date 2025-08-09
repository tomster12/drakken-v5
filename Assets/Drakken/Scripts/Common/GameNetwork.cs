using Drakken.Common.Utility;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;

namespace Drakken.Common
{
    public class TaskManager
    {
        private ulong nextTaskId = 1;
        private readonly Dictionary<ulong, TaskCompletionSource<object>> current = new();

        public (ulong taskId, Task<T> task) Create<T>()
        {
            var taskId = nextTaskId++;
            current[taskId] = new TaskCompletionSource<object>();
            return (taskId, current[taskId].Task.ContinueWith(t => (T)t.Result));
        }

        public void Complete<T>(ulong taskId, T result)
        {
            if (current.TryGetValue(taskId, out var tcs))
            {
                current.Remove(taskId);
                tcs.SetResult(result!);
            }
        }
    }

    public class GameNetwork : NetworkBehaviour
    {
        public static GameNetwork Singleton { get; private set; }

        private TaskManager tasks = new();

        private void Awake()
        {
            Singleton = this;
        }

        public Task<bool> RequestJoinGameAsync()
        {
            var (taskId, task) = tasks.Create<bool>();
            RequestJoinGameServerRpc(taskId);
            return task;
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestJoinGameServerRpc(ulong taskId, ServerRpcParams rpcParams = default)
        {
            var senderId = rpcParams.Receive.SenderClientId;
            var response = GameServer.Singleton.OnRequestJoinGame(senderId);
            RequestJoinGameResponseClientRpc(taskId, response, MakeClientRpcParams(senderId));
        }

        [ClientRpc]
        public void RequestJoinGameResponseClientRpc(ulong taskId, bool response, ClientRpcParams rpcParams = default)
        {
            tasks.Complete(taskId, response);
        }

        private static ClientRpcParams MakeClientRpcParams(ulong id)
        {
            return new() { Send = new() { TargetClientIds = new[] { id } } };
        }
    }
}
