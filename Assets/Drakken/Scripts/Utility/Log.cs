using UnityEngine;

namespace Drakken.Common.Utility
{
    public static class Log
    {
        public static void Info(string tag, string message)
        {
            Debug.Log($"[INFO {tag}] {message}");
        }

        public static void Error(string tag, string message)
        {
            Debug.LogError($"[ERROR {tag}] {message}");
        }
    }
}
