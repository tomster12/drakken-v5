using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace Drakken.Utility
{
    public static class ListExtensions
    {
        public static List<T> ShuffleInplace<T>(this List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
            return list;
        }
    }
}