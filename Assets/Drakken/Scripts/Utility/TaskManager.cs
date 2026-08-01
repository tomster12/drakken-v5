using System.Collections.Generic;
using System.Threading.Tasks;
using Drakken.Common.Utility;

namespace Drakken.Common
{
    internal interface IPendingTaskCompletion
    {
        void Complete(object result);
    }

    internal class PendingTaskCompletion<T> : IPendingTaskCompletion
    {
        private readonly TaskCompletionSource<T> tcs = new();
        public Task<T> Task => tcs.Task;

        public void Complete(object result) => tcs.SetResult((T)result);
    }


    internal class TaskManager
    {
        private ulong nextRequestId = 1;
        private readonly Dictionary<ulong, IPendingTaskCompletion> pendingTasksByRequestId = new();

        public (ulong requestId, Task<T> task) Create<T>()
        {
            var requestId = nextRequestId++;

            var pendingTaskCompletion = new PendingTaskCompletion<T>();

            pendingTasksByRequestId[requestId] = pendingTaskCompletion;

            return (requestId, pendingTaskCompletion.Task);
        }

        public bool Complete<T>(ulong requestId, T result)
        {
            if (pendingTasksByRequestId.TryGetValue(requestId, out var pendingTaskCompletion))
            {
                pendingTasksByRequestId.Remove(requestId);
                pendingTaskCompletion.Complete(result);
                return true;
            }

            return false;
        }
    }
}
