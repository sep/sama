using Dapper;
using Microsoft.Data.SqlClient;
using System.Diagnostics.CodeAnalysis;

namespace sama.Services
{
    /// <summary>
    /// This is a wrapper around SqlConnection, which cannot be (easily) tested.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class SqlConnectionWrapper
    {
        public virtual int Execute(string connectionString, string sql, object? model = null)
        {
            using var sqlConnection = new SqlConnection(connectionString);
            return sqlConnection.Execute(sql, model);
        }
    }
}
