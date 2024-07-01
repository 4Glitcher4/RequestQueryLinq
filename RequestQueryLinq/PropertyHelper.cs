using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace RequestQueryLinq
{
    public static class PropertyHelper
    {
        public static object PropertyTypeConvert<T>(string fieldName, string value)
        {
            var propertyInfo = GetPropertyInfo(typeof(T), fieldName);
            if (propertyInfo == null) throw new ArgumentException($"Field '{fieldName}' not found on type '{typeof(T).Name}'");

            var propertyType = propertyInfo.PropertyType;

            if (propertyType.IsEnum)
            {
                return Enum.Parse(propertyType, value, true);
            }

            if (propertyType == typeof(Guid))
            {
                return Guid.Parse(value);
            }

            if (propertyType.Namespace == nameof(System))
            {
                return Convert.ChangeType(value, propertyType);
            }

            return Convert.ChangeType(value, propertyType);
        }

        public static object PropertyTypeConvert(Type type, string fieldName, string value)
        {
            var propertyInfo = GetPropertyInfo(type, fieldName);
            if (propertyInfo == null) throw new ArgumentException($"Field '{fieldName}' not found on type '{type.Name}'");

            var propertyType = propertyInfo.PropertyType;

            if (propertyType.IsEnum)
            {
                return Enum.Parse(propertyType, value, true);
            }

            if (propertyType == typeof(Guid))
            {
                return Guid.Parse(value);
            }

            if (propertyType.Namespace == nameof(System))
            {
                return Convert.ChangeType(value, propertyType);
            }

            return Convert.ChangeType(value, propertyType);
        }

        public static object PropertyTypeConvert(Type propertyType, string value)
        {
            if (propertyType.IsEnum)
            {
                return Enum.Parse(propertyType, value, true);
            }

            if (propertyType == typeof(Guid))
            {
                return Guid.Parse(value);
            }

            if (propertyType.Namespace == nameof(System))
            {
                return Convert.ChangeType(value, propertyType);
            }

            return Convert.ChangeType(value, propertyType);
        }

        public static PropertyInfo GetPropertyInfo(Type type, string propertyPath)
        {
            PropertyInfo propertyInfo = null;
            foreach (var propertyName in propertyPath.Split('.'))
            {
                propertyInfo = type.GetProperty(propertyName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                if (propertyInfo == null) return null;
                type = propertyInfo.PropertyType;
            }
            return propertyInfo;
        }
    }
}
