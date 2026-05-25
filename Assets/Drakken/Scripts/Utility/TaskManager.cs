using System.Collections.Generic;
using System.Threading.Tasks;
using Drakken.Common.Utility;

namespace Drakken.Common
{
    internal class TaskManager
    {
        private readonly Dictionary<string, TaskCompletionSource<object>> current = new();

        public Task<T> Create<T>(string key)
        {
            Assert.False(current.ContainsKey(key), $"Task '{key}' is already in progress");
            var tcs = new TaskCompletionSource<object>();
            current[key] = tcs;
            return tcs.Task.ContinueWith(t => (T)t.Result);
        }

        public bool Complete<T>(string key, T result)
        {
            if (current.TryGetValue(key, out var tcs))
            {
                current.Remove(key);
                tcs.SetResult(result!);
                return true;
            }
            return false;
        }
    }
}