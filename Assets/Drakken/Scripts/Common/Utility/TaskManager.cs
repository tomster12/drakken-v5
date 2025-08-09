using System.Collections.Generic;
using System.Threading.Tasks;

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
}
