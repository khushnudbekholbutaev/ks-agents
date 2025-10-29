using Common.Interfaces;
using Common.Models;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;

namespace Common.Helpers
{
    public static class DBContexts
    {
        public static string ProjectDataPath = ConfigurationManager.ConfigurationManager.CurrentConfig.DataPath;

        public static string DB_PATH =
            Path.Combine(ProjectDataPath, "KSDatabase.db");

        private static readonly object _lock = new object();

        public static SQLiteConnection CreateConnection()
        {
            lock (_lock)
            {
                if (!Directory.Exists(ProjectDataPath))
                {
                    Directory.CreateDirectory(ProjectDataPath);
                }

                var connectionString = $"Data Source={DB_PATH};Version=3;";
                var connection = new SQLiteConnection(connectionString);
                connection.Open();
                return connection;
            }
        }

        public static void CreateTableIfNotExists<T>()
        {
            lock (_lock)
            {
                using (var connection = CreateConnection())
                {
                    var type = typeof(T);
                    var tableName = type.Name;
                    var props = type.GetProperties();

                    var columns = new List<string>();
                    foreach (var prop in props)
                    {
                        string sqlType = prop.PropertyType == typeof(int) ? "INTEGER"
                                        : prop.PropertyType == typeof(DateTime) ? "TEXT"
                                        : "TEXT";

                        if (prop.Name.Equals("Id", StringComparison.OrdinalIgnoreCase))
                            sqlType += " PRIMARY KEY AUTOINCREMENT";

                        columns.Add($"{prop.Name} {sqlType}");
                    }

                    string sql = $"CREATE TABLE IF NOT EXISTS {tableName} ({string.Join(", ", columns)})";

                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = sql;
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        public static void Insert<T>(T entity)
        {
            lock (_lock)
            {
                using (var connection = CreateConnection())
                {
                    var type = typeof(T);
                    var tableName = type.Name;
                    var properties = type.GetProperties()
                                            .Where(p => p.Name.ToLower() != "id")
                                            .ToArray();

                    var columnNames = string.Join(", ", properties.Select(p => p.Name));
                    var parameterNames = string.Join(", ", properties.Select(p => "@" + p.Name));

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = $"INSERT INTO {tableName} ({columnNames}) VALUES ({parameterNames})";

                        foreach (var prop in properties)
                        {
                            var value = prop.GetValue(entity);

                            if (value is DateTime dt)
                                value = dt.ToString("yyyy-MM-dd HH:mm:ss");

                            command.Parameters.AddWithValue("@" + prop.Name, value ?? DBNull.Value);
                        }

                        command.ExecuteNonQuery();
                    }
                }
            }
        }

        public static void InsertOrUpdateConfig<T>(T entity)
        {
            lock (_lock)
            {
                using (var connection = CreateConnection())
                {
                    var type = typeof(T);
                    var tableName = type.Name;

                    var nameProp = type.GetProperty("Name");
                    var valueProp = type.GetProperty("Value");

                    if (nameProp == null || valueProp == null)
                        throw new InvalidOperationException($"Table {tableName} must contain 'Name' and 'Value' properties.");

                    var nameValue = nameProp.GetValue(entity)?.ToString();
                    var valueValue = valueProp.GetValue(entity) ?? DBNull.Value;

                    if (string.IsNullOrEmpty(nameValue)) return;

                    using (var checkCmd = connection.CreateCommand())
                    {
                        checkCmd.CommandText = $"SELECT COUNT(*) FROM {tableName} WHERE Name = @Name";
                        checkCmd.Parameters.AddWithValue("@Name", nameValue);

                        var count = Convert.ToInt32(checkCmd.ExecuteScalar());

                        if (count > 0)
                        {
                            using (var updateCmd = connection.CreateCommand())
                            {
                                updateCmd.CommandText = $"UPDATE {tableName} SET Value = @Value WHERE Name = @Name";
                                updateCmd.Parameters.AddWithValue("@Value", valueValue);
                                updateCmd.Parameters.AddWithValue("@Name", nameValue);
                                updateCmd.ExecuteNonQuery();
                            }
                        }
                        else
                        {
                            using (var insertCmd = connection.CreateCommand())
                            {
                                insertCmd.CommandText = $"INSERT INTO {tableName} (Name, Value) VALUES (@Name, @Value)";
                                insertCmd.Parameters.AddWithValue("@Name", nameValue);
                                insertCmd.Parameters.AddWithValue("@Value", valueValue);
                                insertCmd.ExecuteNonQuery();
                            }
                        }
                    }
                }
            }
        }
    }
}