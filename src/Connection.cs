using Microsoft.Extensions.Configuration;
using Npgsql;
using System;
using System.Data;
using System.Threading.Tasks;

namespace TestResultDB
{
    /// <summary>
    /// Manages a PostgreSQL database connection using Npgsql. <br />
    /// Provides methods to open and close the connection synchronously.
    /// </summary>
    internal class Connection : IDisposable
    {
        private readonly string _connectionString;
        private NpgsqlConnection? _dbConnection;

        /// <summary>
        /// Constructor.
        /// Constructs a string for a connection estblishment
        /// </summary>
        public Connection()
        {
            var builder = new ConfigurationBuilder();
            // установка пути к текущему каталогу
            builder.SetBasePath(Directory.GetCurrentDirectory());
            // получаем конфигурацию из файла appsettings.json
            builder.AddJsonFile("config.json");
            // создаем конфигурацию
            var config = builder.Build();
            // получаем строку подключения
            _connectionString = config.GetConnectionString("localhost");
        }

        /// <summary>
        /// Get access to the database connection.
        /// </summary>
        /// <exception cref="InvalidOperationException" />
        public NpgsqlConnection GetDbConnection => _dbConnection ?? throw new InvalidOperationException("DB connection not init");

        /// <summary>
        /// Opens connection to the DB
        /// </summary>
        /// <returns>
        /// <c>true</c> - Connection established
        /// </returns>
        public bool openConnection()
        {
            if (_dbConnection == null)
                _dbConnection = new NpgsqlConnection(_connectionString);

            if (_dbConnection.State != ConnectionState.Open)
            {
                try
                {
                    _dbConnection.Open();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to open connection: {ex.Message}");
                    return false;
                }
            }

            return _dbConnection.State == ConnectionState.Open;
        }

        /// <summary>
        /// Closes connection to the DB
        /// </summary>
        /// <returns>
        /// <c>true</c> - Connection closed succesfully
        /// </returns>
        public bool closeConnection()
        {
            if (_dbConnection?.State == ConnectionState.Open)
            {
                try
                {
                    _dbConnection.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to close connection: {ex.Message}");
                    return false;
                }
            }

            return _dbConnection?.State == ConnectionState.Closed;
        }

        /// <summary>
        /// Disposing to release all the resources
        /// </summary>
        public void Dispose()
        {
            if (_dbConnection != null )
                _dbConnection?.Dispose();
        }
    }
}
