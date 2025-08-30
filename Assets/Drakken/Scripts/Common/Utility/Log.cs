using UnityEngine;

namespace Drakken.Common.Utility
{
    public static class Assert
    {
        public static void True(bool condition, string message = null)
        {
            if (!condition)
            {
                Log.Error("Assert", message);
                throw new System.Exception($"Assertion failed: {message}");
            }
        }

        public static void False(bool condition, string message = null)
        {
            if (condition)
            {
                Log.Error("Assert", message);
                throw new System.Exception($"Assertion failed: {message}");
            }
        }

        public static void NotNull<T>(T obj, string message = null) where T : class
        {
            if (obj == null)
            {
                Log.Error("Assert", message);
                throw new System.Exception($"Assertion failed: {message}");
            }
        }
    }

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
