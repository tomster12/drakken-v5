namespace Drakken.Utility
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

        public static void Null<T>(T obj, string message = null) where T : class
        {
            if (obj != null)
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

        public static void NotNullOrEmpty(string obj, string message = null)
        {
            if (string.IsNullOrEmpty(obj))
            {
                Log.Error("Assert", message);
                throw new System.Exception($"Assertion failed: {message}");
            }
        }
    }
}
