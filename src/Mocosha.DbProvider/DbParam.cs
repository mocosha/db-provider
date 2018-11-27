using System.Data;

namespace Mocosha.DbProvider
{
    /// <summary>
    /// Database parameter query
    /// </summary>
    public class DbParam
    {
        private DbParam(SqlDbType type, object value)
        {
            Type = type;
            Value = value;
        }

        private DbParam(SqlDbType type, object value, int size)
        {
            Type = type;
            Value = value;
            Size = size;
        }

        /// <summary>
        /// Creates new instance of DbParam class
        /// </summary>
        /// <param name="type">SQL data type</param>
        /// <param name="value">Value</param>
        /// <returns></returns>
        public static DbParam Create(SqlDbType type, object value)
        {
            return new DbParam(type, value);
        }

        /// <summary>
        /// Creates new instance of DbParam class
        /// </summary>
        /// <param name="type">SQL data type</param>
        /// <param name="value">Value</param>
        /// <param name="size">Size/precision</param>
        /// <returns></returns>
        public static DbParam Create(SqlDbType type, object value, int size)
        {
            return new DbParam(type, value, size);
        }

        /// <summary>
        /// Parameter SQL data type
        /// </summary>
        public SqlDbType Type { get; private set; }
        /// <summary>
        /// Parameter value
        /// </summary>
        public object Value { get; private set; }
        /// <summary>
        /// Parameter size/precision
        /// </summary>
        public int Size { get; private set; }
    }
}
