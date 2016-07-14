using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace NCli
{
    public static class Extensions
    {
        public static bool IsGenericEnumerable(this Type type)
        {
            return typeof(IEnumerable<>).IsAssignableFrom(type);
        }

        public static Type GetEnumerableType(this Type type)
        {
            return type.GetGenericArguments().First();
        }
    }
}
