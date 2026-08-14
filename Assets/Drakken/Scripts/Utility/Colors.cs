using UnityEngine;

namespace Drakken.Utility
{
    public static class Colors
    {
        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var color);
            return color;
        }
    }
}
