using System;
using System.Threading;
using System.Threading.Tasks;

namespace Drakken.Utility
{
    public static class AsyncUtility
    {
        public static async Task DelayTask(int delay, Func<Task> taskFunc, CancellationToken ct)
        {
            await Task.Delay(delay, ct);
            await taskFunc();
        }
    }
}
