using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestSama
{
    public class TestDbConnection : DbConnection
    {
        public override string ConnectionString { get; set; }

        public override string Database => "TestDb";

        public override string DataSource => "TestDataSource";

        public override string ServerVersion => "v1";

        public override ConnectionState State => ConnectionState.Open;

        public override void ChangeDatabase(string databaseName)
        {
            throw new NotImplementedException();
        }

        public override void Close()
        {
        }

        public override void Open()
        {
        }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        {
            throw new NotImplementedException();
        }

        protected override DbCommand CreateDbCommand()
        {
            return new TestDbCommand();
        }


        public class TestDbCommand : DbCommand
        {
            public override string CommandText { get; set; }
            public override int CommandTimeout { get; set; }
            public override CommandType CommandType { get; set; }
            public override bool DesignTimeVisible { get; set; }
            public override UpdateRowSource UpdatedRowSource { get; set; }
            protected override DbConnection DbConnection { get; set; }

            protected override DbParameterCollection DbParameterCollection { get; } = new TestDbParameterCollection();

            protected override DbTransaction DbTransaction { get; set; }

            public override void Cancel()
            {
            }

            public override int ExecuteNonQuery()
            {
                return 1;
            }

            public override object ExecuteScalar()
            {
                return new object();
            }

            public override void Prepare()
            {
            }

            protected override DbParameter CreateDbParameter()
            {
                return new TestDbParameter();
            }

            protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
            {
                throw new NotImplementedException();
            }
        }

        public class TestDbParameter : DbParameter
        {
            public override DbType DbType { get; set; }
            public override ParameterDirection Direction { get; set; }
            public override bool IsNullable { get; set; }
            public override string ParameterName { get; set; }
            public override int Size { get; set; }
            public override string SourceColumn { get; set; }
            public override bool SourceColumnNullMapping { get; set; }
            public override object Value { get; set; }

            public override void ResetDbType()
            {
            }
        }

        public class TestDbParameterCollection : DbParameterCollection
        {
            public List<DbParameter> BackingList { get; } = new List<DbParameter>();

            public override int Count => BackingList.Count;

            public override object SyncRoot => ((ICollection)BackingList).SyncRoot;

            public override int Add(object value)
            {
                BackingList.Add((DbParameter)value);
                return 1;
            }

            public override void AddRange(Array values)
            {
                BackingList.AddRange(values.Cast<DbParameter>());
            }

            public override void Clear()
            {
                BackingList.Clear();
            }

            public override bool Contains(object value)
            {
                return BackingList.Contains((DbParameter)value);
            }

            public override bool Contains(string value)
            {
                return BackingList.Find(p => p.ParameterName == value) != null;
            }

            public override void CopyTo(Array array, int index)
            {
                throw new NotImplementedException();
            }

            public override IEnumerator GetEnumerator()
            {
                return BackingList.GetEnumerator();
            }

            public override int IndexOf(object value)
            {
                return BackingList.IndexOf((DbParameter)value);
            }

            public override int IndexOf(string parameterName)
            {
                return BackingList.FindIndex(p => p.ParameterName == parameterName);
            }

            public override void Insert(int index, object value)
            {
                BackingList.Insert(index, (DbParameter)value);
            }

            public override void Remove(object value)
            {
                BackingList.Remove((DbParameter)value);
            }

            public override void RemoveAt(int index)
            {
                BackingList.RemoveAt(index);
            }

            public override void RemoveAt(string parameterName)
            {
                BackingList.RemoveAt(IndexOf(parameterName));
            }

            protected override DbParameter GetParameter(int index)
            {
                return BackingList[index];
            }

            protected override DbParameter GetParameter(string parameterName)
            {
                return BackingList.First(p => p.ParameterName == parameterName);
            }

            protected override void SetParameter(int index, DbParameter value)
            {
                BackingList[index] = value;
            }

            protected override void SetParameter(string parameterName, DbParameter value)
            {
                BackingList[IndexOf(parameterName)] = value;
            }
        }
    }
}
