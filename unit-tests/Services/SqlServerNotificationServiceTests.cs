using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using sama.Models;
using sama.Services;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace TestSama.Services
{
    [TestClass]
    public class SqlServerNotificationServiceTests
    {
        private ILogger<SqlServerNotificationService> _logger;
        private SettingsService _settings;
        private SqlConnectionWrapper _sqlConnectionWrapper;
        private SqlServerNotificationService _service;

        private DbConnection _dbConnection;
        private DbCommand _dbCommand;

        [TestInitialize]
        public void Setup()
        {
            _logger = Substitute.For<ILogger<SqlServerNotificationService>>();
            _settings = Substitute.For<SettingsService>((IServiceProvider)null);
            _sqlConnectionWrapper = Substitute.For<SqlConnectionWrapper>();

            _service = new SqlServerNotificationService(_logger, _settings, _sqlConnectionWrapper);

            _settings.Notifications_SqlServer_Connection.Returns("conn1");
            _settings.Notifications_SqlServer_TableName.Returns("ta'bl\"e1");

            _dbConnection = Substitute.ForPartsOf<TestDbConnection>();
            _sqlConnectionWrapper.GetSqlConnection("conn1").Returns(_dbConnection);
            _dbCommand = Substitute.ForPartsOf<TestDbConnection.TestDbCommand>();
            _dbConnection.CreateCommand().Returns(_dbCommand);
        }

        [TestMethod]
        public void NotifyMiscShouldSaveRecord()
        {
            _service.NotifyMisc(new Endpoint { Id = 1, Enabled = true, Name = "ep1", Kind = Endpoint.EndpointKind.Icmp }, NotificationType.EndpointAdded);

            _dbCommand.Received().CommandText = string.Format(SqlServerNotificationService.INSERT_SCRIPT, "table1");
            _dbCommand.Received(5).CreateParameter();
            _dbCommand.Received(1).ExecuteNonQuery();
            Assert.AreEqual(1, _dbCommand.Parameters["EndpointId"].Value);
            Assert.AreEqual("ep1", _dbCommand.Parameters["EndpointName"].Value);
            Assert.AreEqual(@"{""enabled"":true,""endpointType"":""Icmp"",""event"":""EndpointAdded"",""downAsOf"":null,""downReason"":null}", _dbCommand.Parameters["JsonMetadata"].Value);
        }

        [TestMethod]
        public void NotifyUpShouldSaveRecordWithoutDowntime()
        {
            _service.NotifyUp(new Endpoint { Id = 1, Enabled = true, Name = "ep1", Kind = Endpoint.EndpointKind.Icmp }, null);

            _dbCommand.Received().CommandText = string.Format(SqlServerNotificationService.INSERT_SCRIPT, "table1");
            _dbCommand.Received(5).CreateParameter();
            _dbCommand.Received(1).ExecuteNonQuery();
            Assert.AreEqual(1, _dbCommand.Parameters["EndpointId"].Value);
            Assert.AreEqual("ep1", _dbCommand.Parameters["EndpointName"].Value);
            Assert.AreEqual(true, _dbCommand.Parameters["IsUp"].Value);
            Assert.AreEqual(@"{""enabled"":true,""endpointType"":""Icmp"",""event"":null,""downAsOf"":null,""downReason"":null}", _dbCommand.Parameters["JsonMetadata"].Value);
        }

        [TestMethod]
        public void NotifyUpShouldSaveRecordWithDowntime()
        {
            _service.NotifyUp(new Endpoint { Id = 1, Enabled = true, Name = "ep1", Kind = Endpoint.EndpointKind.Icmp }, DateTimeOffset.Now.AddMinutes(-5));

            _dbCommand.Received().CommandText = string.Format(SqlServerNotificationService.INSERT_SCRIPT, "table1");
            _dbCommand.Received(5).CreateParameter();
            _dbCommand.Received(1).ExecuteNonQuery();
            Assert.AreEqual(1, _dbCommand.Parameters["EndpointId"].Value);
            Assert.AreEqual("ep1", _dbCommand.Parameters["EndpointName"].Value);
            Assert.AreEqual(true, _dbCommand.Parameters["IsUp"].Value);
            Assert.AreEqual(5, _dbCommand.Parameters["RecordedDowntimeMinutes"].Value);
        }

        [TestMethod]
        public void NotifyDownShouldSaveRecord()
        {
            _service.NotifyDown(new Endpoint { Id = 1, Enabled = true, Name = "ep1", Kind = Endpoint.EndpointKind.Icmp }, DateTimeOffset.Now, new Exception("Test Error Message"));

            _dbCommand.Received().CommandText = string.Format(SqlServerNotificationService.INSERT_SCRIPT, "table1");
            _dbCommand.Received(5).CreateParameter();
            _dbCommand.Received(1).ExecuteNonQuery();
            Assert.AreEqual(1, _dbCommand.Parameters["EndpointId"].Value);
            Assert.AreEqual("ep1", _dbCommand.Parameters["EndpointName"].Value);
            Assert.AreEqual(false, _dbCommand.Parameters["IsUp"].Value);
            StringAssert.Contains((string)_dbCommand.Parameters["JsonMetadata"].Value, @"""downReason"":""Test Error Message""");
        }

        [TestMethod]
        public void ShouldCreateTable()
        {
            _dbCommand.ExecuteNonQuery().Returns(x => { throw new Exception("ex1"); }, x => 1);

            _service.NotifyMisc(new Endpoint { Id = 1, Enabled = true, Name = "ep1", Kind = Endpoint.EndpointKind.Icmp }, NotificationType.EndpointAdded);

            _dbCommand.Received(3).ExecuteNonQuery();

            Received.InOrder(() =>
            {
                _dbCommand.CommandText = string.Format(SqlServerNotificationService.INSERT_SCRIPT, "table1");
                _dbCommand.CommandText = string.Format(SqlServerNotificationService.CREATE_TABLE_SCRIPT, "table1");
                _dbCommand.CommandText = string.Format(SqlServerNotificationService.INSERT_SCRIPT, "table1");
            });
        }

        [TestMethod]
        public void ShouldNotSaveAnyRecordsWhenNotConfigured()
        {
            _settings.Notifications_SqlServer_Connection.Returns("");
            _settings.Notifications_SqlServer_TableName.Returns("a");

            _service.NotifyMisc(new Endpoint { Id = 1, Enabled = true, Name = "ep1", Kind = Endpoint.EndpointKind.Icmp }, NotificationType.EndpointAdded);

            _settings.Notifications_SqlServer_Connection.Returns("a");
            _settings.Notifications_SqlServer_TableName.Returns("");

            _service.NotifyMisc(new Endpoint { Id = 1, Enabled = true, Name = "ep1", Kind = Endpoint.EndpointKind.Icmp }, NotificationType.EndpointAdded);

            _dbConnection.DidNotReceiveWithAnyArgs().CreateCommand();
        }
    }
}
