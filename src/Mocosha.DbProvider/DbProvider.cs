using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace Mocosha.DbProvider
{
    /// <summary>
    /// Database access provider
    /// </summary>
    /// <example>
    /// Update table
    /// <code>
    /// using (var dbProvider = new DbProvider("Connection string name"))
    /// {
    ///     var rowsAffected = dbProvider
    ///     .SetCommandFromText("UPDATE dbo.table SET value=@value")
    ///     .WithParameters(new
    ///     {
    ///         @value = DbParam.Create(SqlDbType.Char, "new value")
    ///     })
    ///     .ExecuteCommand();
    /// }
    /// </code>
    /// </example>
    /// <example>
    /// Select from table
    /// <code>
    /// class RideDetails
    /// {
    ///     public string ConfirmationNo { get; set; }
    ///     public DateTime PickupDateTime { get; set; }
    /// }
    /// 
    /// using (var dbProvider = new DbProvider("Connection string name"))
    /// {
    ///     var listOfRides = dbProvider
    ///         .SetCommandFromText("SELECT confirmation_no ConfirmationNo, req_date_time PickupDateTime FROM rides WHERE affiliate=@affiliateId")
    ///         .WithParameters(new
    ///         {
    ///             @affiliateId = DbParam.Create(SqlDbType.Int, 123)
    ///         })
    ///         .ExecuteQuery&gt;RideDetails&lt;()
    ///         .ToList();
    /// }
    /// </code>
    /// </example>
    public class DbProvider : IDisposable
    {
        /// <summary>
        /// Database access provider
        /// </summary>
        /// <param name="connectionStringName">Connection string name from application config</param>
        public DbProvider(string connectionStringName)
        {
            var connectionString = ConfigurationManager.ConnectionStrings[connectionStringName].ConnectionString;
            _connection = new SqlConnection(connectionString);
        }

        /// <summary>
        /// Sets SQL command from provided text
        /// </summary>
        /// <param name="commandText">SQL command text</param>
        /// <returns></returns>
        public DbProvider SetCommandFromText(string commandText)
        {
            CreateNewCommand();
            _command.CommandText = commandText;
            return this;
        }

        /// <summary>
        /// Sets SQL command from provided text and sets command type to StoredProcedure
        /// </summary>
        /// <param name="storedProcedureName">Stored procedure name</param>
        /// <returns></returns>
        public DbProvider SetStoredProcedureFromText(string storedProcedureName)
        {
            SetCommandFromText(storedProcedureName);
            _command.CommandType = CommandType.StoredProcedure;
            return this;
        }

        /// <summary>
        /// Adds parameters to existing SQL command
        /// </summary>
        /// <param name="parameters">Object with properties of type <see cref="DbParam">DbParam</see>. Property names are used as parameter names for the command</param>
        /// <returns></returns>
        public DbProvider WithParameters(dynamic parameters)
        {
            EnsureCommandIsNotNull();

            IEnumerable<KeyValuePair<string, object>> properties = ReflectionExtensions.GetObjectProperties(parameters);

            if (!properties.All(p => p.Value is DbParam))
            {
                throw new InvalidParameterTypeException("Not all parameters are of type DbParam");
            }

            var parametersDictionary = properties.ToDictionary(property => property.Key, property => (DbParam)property.Value);

            return WithDictionaryParameters(parametersDictionary);
        }

        /// <summary>
        /// Adds parameter to existing SQL command
        /// </summary>
        /// <param name="parameters">Dictionary of parameters. </param>
        /// <returns></returns>
        public DbProvider WithDictionaryParameters(IDictionary<string, DbParam> parameters)
        {
            EnsureCommandIsNotNull();

            foreach (var prop in parameters)
            {
                var name = prop.Key;
                var parameter = prop.Value;

                var dbType = parameter.Type;
                var dbValue = parameter.Value;

                if (dbValue == null)
                {
                    _command.Parameters.Add(name, dbType).Value = DBNull.Value;
                    continue;
                }

                if (parameter.Size > 0)
                {
                    _command.Parameters.Add(name, dbType, parameter.Size).Value = dbValue;
                }
                else if (dbType == SqlDbType.VarChar || dbType == SqlDbType.NVarChar
                    || dbType == SqlDbType.Text || dbType == SqlDbType.NText
                    || dbType == SqlDbType.Char || dbType == SqlDbType.NChar)
                {
                    var value = dbValue.ToString();
                    _command.Parameters.Add(name, dbType, value.Length).Value = value;
                }
                else
                {
                    _command.Parameters.Add(name, dbType).Value = dbValue;
                }
            }

            return this;
        }

        /// <summary>
        /// Sets timeout to existing command
        /// </summary>
        /// <param name="timeout">Timeout in miliseconds</param>
        /// <returns></returns>
        public DbProvider WithTimeout(int timeout)
        {
            EnsureCommandIsNotNull();

            _command.CommandTimeout = timeout;
            return this;
        }

        /// <summary>
        /// Starts new transaction
        /// </summary>
        /// <returns></returns>
        public DbProvider BeginTransaction()
        {
            OpenConnection();
            _transaction = _connection.BeginTransaction();

            if (_command != null)
                _command.Transaction = _transaction;
            return this;
        }

        /// <summary>
        /// Commits transaction if active
        /// </summary>
        /// <returns></returns>
        public DbProvider CommitTransaction()
        {
            if (_transaction != null)
            {
                _transaction.Commit();
                _transaction.Dispose();
                _transaction = null;
            }
            return this;
        }

        /// <summary>
        /// Rollbacks transaction if active
        /// </summary>
        /// <returns></returns>
        public DbProvider RollbackTransaction()
        {
            if (_transaction != null)
            {
                _transaction.Rollback();
                _transaction.Dispose();
                _transaction = null;
            }
            return this;
        }

        /// <summary>
        /// Executes non query command
        /// </summary>
        /// <returns>Number of rows affected</returns>
        public int ExecuteCommand()
        {
            EnsureCommandIsNotNull();

            OpenConnection();
            return _command.ExecuteNonQuery();
        }

        /// <summary>
        /// Executes query to get data
        /// </summary>
        /// <typeparam name="T">Type of result. If reference type is provided then properties will be populated by matching property names with result set column name.</typeparam>
        /// <returns>IEnumerable of <see cref="T">object</see></returns>
        public IEnumerable<T> ExecuteQuery<T>()
        {
            EnsureCommandIsNotNull();

            OpenConnection();
            using (SqlDataReader dr = _command.ExecuteReader())
            {
                if (!dr.HasRows)
                {
                    yield break;
                }

                while (dr.Read())
                {
                    if (typeof(T).IsValueType || typeof(T) == typeof(string))
                    {
                        yield return (T)Convert.ChangeType(dr[0], typeof(T));
                        continue;
                    }

                    var dict = new Dictionary<string, object>();

                    for (int i = 0; i < dr.FieldCount; i++)
                        dict.Add(dr.GetName(i), dr[i]);

                    var obj = Activator.CreateInstance<T>();
                    obj.SetObjectProperties(dict);

                    yield return obj;
                }
            }
        }

        /// <summary>
        /// Executes query to get data
        /// </summary>
        /// <returns>IEnumerable of dictionaries where each dictionary represents one row from result set with column name as a key</returns>
        public IEnumerable<Dictionary<string, object>> ToArrayOfDictionaries()
        {
            EnsureCommandIsNotNull();

            OpenConnection();
            using (SqlDataReader dr = _command.ExecuteReader())
            {
                if (!dr.HasRows)
                {
                    yield break;
                }

                while (dr.Read())
                {
                    var dict = new Dictionary<string, object>();

                    for (var i = 0; i < dr.FieldCount; i++)
                        dict.Add(dr.GetName(i), dr[i] == DBNull.Value ? null : dr[i]);

                    yield return dict;
                }
            }
        }

        private void OpenConnection()
        {
            if (_connection.State != ConnectionState.Open)
            {
                _connection.Open();
            }
        }

        private void CreateNewCommand()
        {
            if (_command != null)
                _command.Dispose();

            _command = new SqlCommand { Connection = _connection };
            if (_transaction != null)
                _command.Transaction = _transaction;
        }

        private void EnsureCommandIsNotNull()
        {
            if (_command == null)
            {
                throw new CommandNotCreatedException("Command not created. Call SetCommandFromTest or SetStoredProcedureFromText before.");
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                if (_command != null)
                    _command.Dispose();
                if (_transaction != null)
                    _transaction.Dispose();
                _connection.Dispose();
            }

            _disposed = true;
        }

        private readonly SqlConnection _connection;
        private SqlCommand _command;
        private SqlTransaction _transaction;
        private bool _disposed;
    }
}
