using UnityEngine;

namespace Drakken.Common.Utility
{
    public static class Assert
    {
        public static void True(bool condition, string message)
        {
            if (!condition)
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
            Debug.Log($"{tag} [INFO]: {message}");
        }

        public static void Error(string tag, string message)
        {
            Debug.LogError($"{tag} [ERROR]: {message}");
        }
    }
}
