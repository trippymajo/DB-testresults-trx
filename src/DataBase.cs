using Microsoft.Extensions.Configuration;
using Npgsql;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;

namespace TestResultDB
{
    public class DataBase : IDisposable
    {
        private NpgsqlConnection _dbConnection;
        const short EXPECTED_MIN_NUM_TABLES = 4;

        public DataBase(string branch, string version, string timestamp)
        {
            //Подключаемся к БД
            try
            {
                _dbConnection = new NpgsqlConnection(GetConnectionString("localhost"));
                _dbConnection.Open();
                //Проверяем было ли выполнено подключение к ДБ
                if (_dbConnection.State != ConnectionState.Open)
                {
                    Console.WriteLine("The connection was not open. Exiting...");
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to connect to the DB: {ex.Message}");
                throw ex;
            }

            //Проверяем табицы в БД, если они отсутствуют, то создаем их
            if (!IsTablesExist())
            {
                CreateTables();
                InsertBranch(branch);
            }
        }

        /// <summary>
        /// Creates tables, rewrites DB tables and fill them with initial values
        /// </summary>
        public void CreateTables()
        {
            string[] commands =
            {
                // Define ENUM for results
                "DROP TYPE IF EXISTS result_enum CASCADE",
                "CREATE TYPE result_enum AS ENUM ('Passed', 'Failed', 'Timeout')",
                //
                // Table: branches [id, branch, status]
                "DROP TABLE IF EXISTS branches CASCADE",
                @"CREATE TABLE branches
                (
                    id SERIAL PRIMARY KEY,
                    branch VARCHAR(25) NOT NULL,
                    status BOOLEAN NOT NULL DEFAULT false
                )",
                "CREATE INDEX idx_branches_name ON branches(branch)",
                //
                // Table: test_runs [id, branch, version, time]
                "DROP TABLE IF EXISTS test_runs CASCADE",
                @"CREATE TABLE test_runs 
                (
                    id SERIAL PRIMARY KEY,
                    branch_id INTEGER NOT NULL,
                    version VARCHAR(25) NOT NULL,
                    time TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (branch_id) REFERENCES branches(id) ON DELETE CASCADE
                )",
                "CREATE INDEX idx_test_runs_branch_id ON test_runs(branch_id)",
                //
                // Table: tests [id, name, category, description]
                "DROP TABLE IF EXISTS tests CASCADE",
                @"CREATE TABLE tests
                (
                    id SERIAL PRIMARY KEY,
                    name VARCHAR(255) NOT NULL UNIQUE,
                    description TEXT
                )",
                "CREATE UNIQUE INDEX idx_tests_name ON tests(name)",
                //
                // Table: results [id, test_run_id, test_id, result, ErrMsg]
                "DROP TABLE IF EXISTS results CASCADE",
                @"CREATE TABLE results 
                (
                    id SERIAL PRIMARY KEY,
                    test_run_id INTEGER NOT NULL,
                    test_id INTEGER NOT NULL,
                    result result_enum NOT NULL,
                    ErrMsg TEXT,
                    FOREIGN KEY (test_run_id) REFERENCES test_runs(id) ON DELETE CASCADE,
                    FOREIGN KEY (test_id) REFERENCES tests(id) ON DELETE CASCADE
                )",
                "CREATE INDEX idx_results_test_run_id ON results(test_run_id)",

            };

            using var transaction = _dbConnection.BeginTransaction();
            using var cmd = new NpgsqlCommand { Connection = _dbConnection, Transaction = transaction };

            try
            {
                foreach (var command in commands)
                {
                    cmd.CommandText = command;
                    cmd.ExecuteNonQuery();
                }
                transaction.Commit();
                Console.WriteLine("All tables created successfully.");
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                Console.WriteLine($"Error occurred while creating tables: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Gets dictionary of testNames + testId from the tests table.
        /// </summary>
        /// <returns>Dictionary of testNames + testId</returns>
        /// <exception cref="ArgumentOutOfRangeException" />
        public Dictionary<string, int> GetTests() //Получить id-шки тестов из БД
        {
            var retVal = new Dictionary<string, int>();
            string sqlQuery = "SELECT id, name FROM tests";

            using var cmd = new NpgsqlCommand(sqlQuery, _dbConnection);
            using var rdr = cmd.ExecuteReader();

            try
            {
                while (rdr.Read())
                {
                    string testName = rdr["name"].ToString();
                    int testId = (int)rdr["id"];
                    //int.TryParse(rdr.GetString(0), out int testid);

                    retVal[testName] = testId;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting tests: {ex.Message}");
                throw;
            }

            return retVal;
        }

        /// <summary>
        /// Branch's Id stored in DB for specific branch name
        /// </summary>
        /// <param name="branch">Tested branch name (Variable:Test_ver)</param>
        /// <returns>
        /// [-1] - Branch does not exits need to insert new branch and handle old branch status<br />
        /// </returns>
        /// <exception cref="ArgumentNullException" />
        public int GetBranchId(string branch)
        {
            if (string.IsNullOrWhiteSpace(branch))
                throw new ArgumentNullException(nameof(branch), "Branch cannot be null or empty");

            int retVal = -1;
            string sqlQuery = "SELECT id FROM branches WHERE branch = @val";
            using var cmd = new NpgsqlCommand(sqlQuery, _dbConnection);
            cmd.Parameters.AddWithValue("val", branch);

            try
            {
                retVal = Convert.ToInt32(cmd.ExecuteScalar());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking branch existence: {ex.Message}");
                throw;
            }

            return retVal;
        }

        /// <summary>
        /// Checks if branch is active and ready for results write
        /// </summary>
        /// <param name="branch">Tested branch name (Variable:Test_ver)</param>
        /// <returns>
        /// [true] - Branch is active and ready to write resources into table results <br />
        /// [false] - Branch is in archive and no automatic result inssertion avilible
        /// </returns>
        /// <exception cref="ArgumentNullException" />
        public bool IsActiveBranch(string branch)
        {
            if (string.IsNullOrWhiteSpace(branch))
                throw new ArgumentNullException(nameof(branch), "Branch cannot be null or empty");

            bool retVal = false;
            string sqlQuery = "SELECT status FROM branches WHERE branch = @val";
            using var cmd = new NpgsqlCommand(sqlQuery, _dbConnection);
            cmd.Parameters.AddWithValue("val", branch);

            try
            {
                retVal = Convert.ToBoolean(cmd.ExecuteScalar());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking is branch active: {ex.Message}");
                throw;
            }

            return retVal;
        }

        /// <summary>
        /// Reutrns id and the branch's name for the oldest active branch in DB.
        /// </summary>
        /// <returns>
        /// Tuple (int id, string branch) <br />
        /// [id] - id of the min active branch <br />
        /// [string] - name of the min active branch <br />
        /// [null] - min active branch not found
        /// </returns>
        public (int id, string branch)? GetMinActiveBranch()
        {
            string sqlQuery = @"
            SELECT id, branch
            FROM branches
            WHERE status = true
            ORDER BY CAST(REGEXP_REPLACE(branch, '[^0-9]', '', 'g') AS INTEGER)
            LIMIT 1;";

            using var cmd = new NpgsqlCommand(sqlQuery, _dbConnection);
            using var rdr = cmd.ExecuteReader();

            try
            {
                if (rdr.Read())
                {
                    int id = rdr.GetInt32(0);
                    string branch = rdr.GetString(1);
                    return (id, branch);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting minimal active branch: {ex.Message}");
                throw;
            }

            return null;
        }

        /// <summary>
        /// Inserts new branch with name in to branches table, returns it's id
        /// </summary>
        /// <param name="branch">Name of the inserting branch</param>
        /// <returns>
        /// [-1] - something wrong, no id reurned.
        /// </returns>
        /// <exception cref="ArgumentNullException"></exception>
        public int InsertBranch(string branch)
        {
            if (string.IsNullOrWhiteSpace(branch))
                throw new ArgumentNullException(nameof(branch), "Branch cannot be null or empty");

            int branchId = -1;
            using var transaction = _dbConnection.BeginTransaction();
            using var cmd = new NpgsqlCommand { Connection = _dbConnection, Transaction = transaction };

            try
            {
                cmd.CommandText = "INSERT INTO branches(branch, status) VALUES(@val, true) RETURNING id";
                cmd.Parameters.AddWithValue("val", branch);
                branchId = Convert.ToInt32(cmd.ExecuteScalar());
                transaction.Commit();
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                Console.WriteLine($"Rollback. Error inserting new branch: {ex.Message}");
                throw;
            }

            return branchId;
        }

        /// <summary>
        /// Deletes archived data of the specified branch from active results table.
        /// </summary>
        /// <param name="branch">Branch name to delete from results</param>
        private void DeleteArchivedFromActive(string branch)
        {
            string sqlCommand = @"
                DELETE FROM results
                WHERE test_run_id IN 
                (
                    SELECT tr.id
                    FROM test_runs tr
                    JOIN branches b ON tr.branch_id = b.id
                    WHERE b.branch = @val
                 )";

            using var transaction = _dbConnection.BeginTransaction();
            using var cmd = new NpgsqlCommand { CommandText = sqlCommand, Connection = _dbConnection, Transaction = transaction };
            cmd.Parameters.AddWithValue("val", branch);

            try
            {
                cmd.ExecuteNonQuery();
                transaction.Commit();

                Console.WriteLine($"Successfully deleted results for branch '{branch}'.");
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                Console.WriteLine($"Error deleting results for branch '{branch}': {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Changes status of the branch in branches to inactive
        /// </summary>
        /// <param name="branch">Branch name to make inactive</param>
        private void MakeBranchInactive(string branch)
        {
            string sqlCommand = "UPDATE branches SET status = false WHERE branch = @val";

            using var transaction = _dbConnection.BeginTransaction();
            using var cmd = new NpgsqlCommand(sqlCommand, _dbConnection, transaction);
            cmd.Parameters.AddWithValue("val", branch);

            try
            {
                int rowsProcessed = cmd.ExecuteNonQuery();
                transaction.Commit();

                if (rowsProcessed > 0)
                    Console.WriteLine($"Branch '{branch}' marked as inactive.");
                else
                    Console.WriteLine($"Branch '{branch}' not found or already inactive.");
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                Console.WriteLine($"Error marking branch '{branch}' inactive: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Iserts archived data of the related branch into archived table.
        /// </summary>
        /// <param name="branch">Branch name to get results from</param>
        /// <param name="tableName">Table name of the archived results</param>
        private void InsertArchivedData(string branch, string tableName)
        {
            string sqlCommand = $@"
                INSERT INTO {tableName} (version, time, test_name, result, ErrMsg)
                SELECT
                    tr.version,
                    tr.time,
                    t.name,
                    r.result,
                    r.ErrMsg
                FROM results r
                JOIN test_runs tr ON r.test_run_id = tr.id
                JOIN branches b ON tr.branch_id = b.id
                JOIN tests t ON r.test_id = t.id
                WHERE b.branch = @val";

            using var transaction = _dbConnection.BeginTransaction();
            using var cmd = new NpgsqlCommand { CommandText = sqlCommand, Connection = _dbConnection, Transaction = transaction };
            cmd.Parameters.AddWithValue("val", branch);

            try
            {
                cmd.ExecuteNonQuery();
                transaction.Commit();
                Console.WriteLine($"Archived results for branch '{branch}' successfully.");
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                Console.WriteLine($"Error archiving results for branch '{branch}': {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Creates archived table with specified name
        /// </summary>
        /// <param name="tableName">Table name for creating archived results</param>
        private void CreateArchivedTable(string tableName)
        { // I dont really like to pass branch multiple times. Static class is a mistake.
            string sqlCommand = $@"
                CREATE TABLE {tableName} (
                    id SERIAL PRIMARY KEY,
                    version VARCHAR(25) NOT NULL,
                    time TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    test_name VARCHAR(255) NOT NULL,
                    result result_enum NOT NULL,
                    ErrMsg TEXT
                )";

            using var transaction = _dbConnection.BeginTransaction();
            using var cmd = new NpgsqlCommand { CommandText = sqlCommand, Connection = _dbConnection, Transaction = transaction };

            try
            {
                cmd.ExecuteNonQuery();
                transaction.Commit();
                Console.WriteLine($"Archived Table '{tableName}' created successfully.");
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                Console.WriteLine($"Error creating archived table '{tableName}': {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Processes archiving branch's results with Creating, Inserting, Deleting steps.
        /// </summary>
        /// <param name="branch">Branch name to archive</param>
        /// <exception cref="ArgumentNullException"></exception>
        public void ArchiveBranch(string branch)
        {
            if (string.IsNullOrWhiteSpace(branch))
                throw new ArgumentNullException(nameof(branch), "Branch cannot be null or empty");

            string tableName = $"archived_{branch.Replace("'", "''").Replace("\"", "")}";

            try
            {
                // Step 1. Create table results_$BRANCH
                CreateArchivedTable(tableName);

                // Step 2. Insert results of the specific branch into results_$BRANCH
                InsertArchivedData(branch, tableName);

                // Step 3. Delete copied results.
                DeleteArchivedFromActive(branch);

                // Step 4. Make branch as inactive.
                MakeBranchInactive(branch);
            }
            catch ( Exception ex )
            {
                Console.WriteLine($"Error occured while archiving branch's results '{tableName}': {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Insert new test run into test_runs table
        /// </summary>
        /// <param name="branchId">Tested branch ID</param>
        /// <param name="ver">Version of the product</param>
        /// <param name="timestamp">Date when the test was ran</param>
        /// <returns>TestRunId of the inserted testRun <br />
        /// [-1] - Error occured
        /// </returns>
        /// <exception cref="ArgumentNullException" />
        public int InsertTestRun(int branchId, string ver, DateTime timestamp)
        {
            if (branchId < 0)
                new ArgumentOutOfRangeException(nameof(branchId), "branchId must be a positive int");
            if (string.IsNullOrWhiteSpace(ver))
                throw new ArgumentNullException(nameof(ver), "Version cannot be null or empty");
            if (timestamp == null)
                throw new ArgumentNullException(nameof(timestamp), "Date cannot be null or empty");

            int testRunId = -1;
            using var transaction = _dbConnection.BeginTransaction();
            using var cmd = new NpgsqlCommand { Connection = _dbConnection, Transaction = transaction };

            try
            {
                cmd.CommandText = "INSERT INTO test_runs(branch_id, version, time) VALUES(@val1, @val2, @val3) RETURNING id";
                cmd.Parameters.AddWithValue("val1", branchId);
                cmd.Parameters.AddWithValue("val2", ver);
                cmd.Parameters.AddWithValue("val3", timestamp);
                testRunId = Convert.ToInt32(cmd.ExecuteScalar());
                transaction.Commit();
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                Console.WriteLine($"Rollback. Error inserting TestRun: {ex.Message}");
                throw;
            }

            return testRunId;
        }

        /// <summary>
        /// Insert new result in to results table
        /// </summary>
        /// <param name="testRunId">test_run_id of the run</param>
        /// <param name="testId">test_id of the completed test</param>
        /// <param name="result">Result of the test</param>
        /// <exception cref="ArgumentNullException" />
        /// <exception cref="ArgumentOutOfRangeException" />
        public void InsertResult(int testRunId, int testId, string result)
        {
            if (testRunId <= 0) 
                throw new ArgumentOutOfRangeException(nameof(testRunId), "testRunId must be a positive int");
            if (testId <= 0) 
                throw new ArgumentOutOfRangeException(nameof(testId), "testId must be a positive int");
            if (string.IsNullOrWhiteSpace(result))
                throw new ArgumentNullException(nameof(result), "Result cannot be null or empty");

            using var transaction = _dbConnection.BeginTransaction();
            using var cmd = new NpgsqlCommand { Connection = _dbConnection, Transaction = transaction };

            try
            {
                cmd.CommandText = "INSERT INTO results (test_run_id, test_id, result) VALUES (@val1, @val2, @val3::result_enum)";
                cmd.Parameters.AddWithValue("val1", testRunId);
                cmd.Parameters.AddWithValue("val2", testId);
                cmd.Parameters.AddWithValue("val3", result);
                cmd.ExecuteNonQuery();
                transaction.Commit();
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                Console.WriteLine($"Rollback. Error inserting result: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Adds test into tests table
        /// </summary>
        /// <param name="testName">Name of the test to insert</param>
        /// <returns>TestId of the inserted testName<br />
        /// [-1] Error occured
        /// </returns>
        /// <exception cref="ArgumentNullException" />
        public int InsertTest(string testName)
        {
            if (string.IsNullOrWhiteSpace(testName))
                throw new ArgumentNullException(nameof(testName), "testName can't be null or empty");

            int testId = -1;
            using var transaction = _dbConnection.BeginTransaction();
            using var cmd = new NpgsqlCommand { Connection = _dbConnection, Transaction = transaction };

            try
            {
                cmd.CommandText = "INSERT INTO tests(name) VALUES(@val) ON CONFLICT (name) DO NOTHING RETURNING id";
                cmd.Parameters.AddWithValue("val", testName);
                testId = Convert.ToInt32(cmd.ExecuteScalar());
                transaction.Commit();
            }
            catch(Exception ex)
            {
                transaction.Rollback();
                Console.WriteLine($"Rollback. Error inserting test: {ex.Message}");
                throw;
            }

            return testId;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="testName"></param>
        /// <param name="versionId"></param>
        /// <returns>
        /// Id number for current testsName from exact version from tests table <br />
        /// [-1] - Error occured wile getting testId
        /// </returns>
        /// <exception cref="ArgumentNullException" />
        /// <exception cref="ArgumentOutOfRangeException" />
        public int GetTestID(string testName, int versionId)
        {
            if (string.IsNullOrWhiteSpace(testName))
                throw new ArgumentNullException(nameof(testName), "testName can't be null or empty");
            if (versionId <= 0)
                throw new ArgumentOutOfRangeException(nameof(versionId), "versionId must be a positive int");

            int testId = -1;
            using var cmd = new NpgsqlCommand { Connection = _dbConnection };

            try
            {
                cmd.CommandText = "SELECT id FROM tests WHERE name = @val";
                cmd.Parameters.AddWithValue("val", testName);
                using NpgsqlDataReader rdr = cmd.ExecuteReader();
                rdr.Read();
                testId = rdr.GetInt32(0);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting testId: {ex.Message}");
                throw;
            }

            return testId;
        }

        /// <summary>
        /// Gets versionID from the versions table. If not exists, adds new version.
        /// </summary>
        /// <param name="version">Version number to add/find in table</param>
        /// <returns>Id of the version in versions table</returns>
        /// <exception cref="ArgumentNullException" />
        public int GetVersionID(string version)
        {
            if (string.IsNullOrWhiteSpace(version)) 
                throw new ArgumentNullException(nameof(version), "Version can not be empty!");

            int versionId = -1;
            using var transaction = _dbConnection.BeginTransaction();
            using var cmd = new NpgsqlCommand { Connection = _dbConnection , Transaction = transaction };

            try
            {
                cmd.CommandText = "SELECT id FROM versions WHERE version = @val";
                cmd.Parameters.AddWithValue("val", version);
                var result = cmd.ExecuteScalar();

                if (result != null) // Try get versionId from the table
                    versionId = Convert.ToInt32(result);
                else // Write new versionId
                {
                    cmd.CommandText = "INSERT INTO versions(version) VALUES(@val) RETURNING id";
                    cmd.Parameters.AddWithValue("val", version);
                    versionId = Convert.ToInt32(cmd.ExecuteScalar());
                }
                transaction.Commit();
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                Console.WriteLine($"Error getting version ID: {ex.Message}");
                throw;
            }

            return versionId;
        }

        /// <summary>
        /// Checks if the DB non empty and already got tables inside it
        /// </summary>
        /// <returns>
        /// [true] - DB is not empty inside
        /// </returns>
        public bool IsTablesExist()
        {
            //TO DO 
            string sqlQuery = @"
                SELECT COUNT(1) 
                FROM information_schema.tables
                WHERE table_schema = 'public'";

            using var cmd = new NpgsqlCommand(sqlQuery, _dbConnection);

            try
            {
                return Convert.ToInt16(cmd.ExecuteScalar()) >= EXPECTED_MIN_NUM_TABLES;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking tables: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get ConnectionString from config JSON file
        /// </summary>
        /// <returns>
        /// ConnectionString
        /// </returns>
        private static string GetConnectionString(string settings)
        {
            var builder = new ConfigurationBuilder();
            // установка пути к текущему каталогу
            builder.SetBasePath(Directory.GetCurrentDirectory());
            // получаем конфигурацию из файла appsettings.json
            builder.AddJsonFile("config.json");
            // создаем конфигурацию
            var config = builder.Build();
            // получаем строку подключения
            return config.GetConnectionString(settings);
        }

        /// <summary>
        /// Disposing to release all the resources
        /// </summary>
        public void Dispose()
        {
            if (_dbConnection != null)
                _dbConnection?.Dispose();
        }
    }
}

