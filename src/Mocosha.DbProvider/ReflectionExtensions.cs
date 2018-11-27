using System;
using System.Collections.Generic;

namespace Mocosha.DbProvider
{
    /// <summary>
    /// Generic extensions
    /// </summary>
    public static class ReflectionExtensions
    {
        /// <summary>
        /// Gets object properties as array of KeyValuePair object
        /// </summary>
        /// <typeparam name="T">Current object type</typeparam>
        /// <param name="obj">Current object</param>
        /// <returns>IEnumerable of KeyValuePair where key is property name</returns>
        public static IEnumerable<KeyValuePair<string, object>> GetObjectProperties<T>(this T obj)
        {
            foreach (var pi in obj.GetType().GetProperties())
            {
                var prop = obj.GetType().GetProperty(pi.Name);
                object value = prop.PropertyType.IsEnum
                    ? prop.GetValue(obj).ToString()
                    : prop.GetValue(obj);

                yield return new KeyValuePair<string, object>
                    (pi.Name, value);
            }
        }

        /// <summary>
        /// Gets object property value
        /// </summary>
        /// <typeparam name="T">Current object type</typeparam>
        /// <param name="obj">Current object</param>
        /// <param name="propertyName">Propery name</param>
        /// <returns>Value of the property</returns>
        public static object GetObjectProperty<T>(this T obj, string propertyName)
        {
            var pi = obj.GetType().GetProperty(propertyName);

            if (pi == null)
                return null;

            return pi.PropertyType.IsEnum
                ? pi.GetValue(obj).ToString()
                : pi.GetValue(obj);
        }

        /// <summary>
        /// Sets value to object properties
        /// </summary>
        /// <typeparam name="T">Current object type</typeparam>
        /// <param name="obj">Current object</param>
        /// <param name="properties">IEnumerable of KeyValuePair where key is property name and value property value</param>
        public static void SetObjectProperties<T>(this T obj, IEnumerable<KeyValuePair<string, object>> properties)
        {
            foreach (var prop in properties)
                SetObjectProperty(obj, prop.Key, prop.Value);
        }

        /// <summary>
        /// Sets value to object property
        /// </summary>
        /// <typeparam name="T">Current object type</typeparam>
        /// <param name="obj">Current object</param>
        /// <param name="propertyName">Property name</param>
        /// <param name="propertyValue">Property value</param>
        public static void SetObjectProperty<T>(this T obj, string propertyName, object propertyValue)
        {
            var pi = obj.GetType().GetProperty(propertyName);

            if (pi == null || !pi.CanWrite)
                return;

            if (propertyValue is DBNull)
                propertyValue = null;

            var type = pi.PropertyType;

            if (propertyValue == null)
            {
                pi.SetValue(obj, null, null);
            }
            else
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof (Nullable<>))
                {
                    var nullType = Nullable.GetUnderlyingType(type);

                    pi.SetValue(obj,
                        nullType.IsEnum
                            ? Convert.ChangeType(Enum.ToObject(nullType, propertyValue), nullType)
                            : Convert.ChangeType(propertyValue, nullType)
                        , null);
                }
                else
                {
                    pi.SetValue(obj,
                        type.IsEnum
                            ? Enum.ToObject(type, propertyValue)
                            : Convert.ChangeType(propertyValue, pi.PropertyType)
                        , null);

                }
            }

        }
    }
}