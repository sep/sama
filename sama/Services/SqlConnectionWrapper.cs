using Microsoft.Data.SqlClient;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace sama.Services
{
    /// <summary>
    /// This is a wrapper around SqlConnection, which cannot be (easily) tested.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class SqlConnectionWrapper
    {
        public virtual DbConnection GetSqlConnection(string connectionString)
        {
            return new SqlConnection(connectionString);
        }
    }
}
