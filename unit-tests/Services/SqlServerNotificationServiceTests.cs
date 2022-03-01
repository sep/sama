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

        [TestInitialize]
        public void Setup()
        {
            _logger = Substitute.For<ILogger<SqlServerNotificationService>>();
            _settings = Substitute.For<SettingsService>((IServiceProvider)null);
            _sqlConnectionWrapper = Substitute.For<SqlConnectionWrapper>();

            _service = new SqlServerNotificationService(_logger, _settings, _sqlConnectionWrapper);

            _settings.Notifications_SqlServer_Connection.Returns("conn1");
            _settings.Notifications_SqlServer_TableName.Returns("ta'bl\"e1");
        }

        [TestMethod]
        public void NotifyMiscShouldSaveRecord()
        {
            SqlServerNotificationService.DbModel model = new();
            _sqlConnectionWrapper.Execute(Arg.Any<string>(), Arg.Any<string>(), Arg.Do<object>(o => model = (SqlServerNotificationService.DbModel)o));

            _service.NotifyMisc(new Endpoint { Id = 1, Enabled = true, Name = "ep1", Kind = Endpoint.EndpointKind.Icmp }, NotificationType.EndpointAdded);

            _sqlConnectionWrapper.Received(1).Execute("conn1", string.Format(SqlServerNotificationService.INSERT_SCRIPT, "table1"), Arg.Any<object>());
            Assert.AreEqual(1, model.EndpointId);
            Assert.AreEqual("ep1", model.EndpointName);
            Assert.AreEqual(@"{""enabled"":true,""endpointType"":""Icmp"",""event"":""EndpointAdded"",""downAsOf"":null,""downReason"":null}", model.JsonMetadata);
        }

        [TestMethod]
        public void NotifyUpShouldSaveRecordWithoutDowntime()
        {
            SqlServerNotificationService.DbModel model = new();
            _sqlConnectionWrapper.Execute(Arg.Any<string>(), Arg.Any<string>(), Arg.Do<object>(o => model = (SqlServerNotificationService.DbModel)o));

            _service.NotifyUp(new Endpoint { Id = 1, Enabled = true, Name = "ep1", Kind = Endpoint.EndpointKind.Icmp }, null);

            _sqlConnectionWrapper.Received(1).Execute("conn1", string.Format(SqlServerNotificationService.INSERT_SCRIPT, "table1"), Arg.Any<object>());
            Assert.AreEqual(1, model.EndpointId);
            Assert.AreEqual("ep1", model.EndpointName);
            Assert.IsTrue(model.IsUp);
            Assert.AreEqual(@"{""enabled"":true,""endpointType"":""Icmp"",""event"":null,""downAsOf"":null,""downReason"":null}", model.JsonMetadata);
        }

        [TestMethod]
        public void NotifyUpShouldSaveRecordWithDowntime()
        {
            SqlServerNotificationService.DbModel model = new();
            _sqlConnectionWrapper.Execute(Arg.Any<string>(), Arg.Any<string>(), Arg.Do<object>(o => model = (SqlServerNotificationService.DbModel)o));

            _service.NotifyUp(new Endpoint { Id = 1, Enabled = true, Name = "ep1", Kind = Endpoint.EndpointKind.Icmp }, DateTimeOffset.Now.AddMinutes(-5));

            _sqlConnectionWrapper.Received(1).Execute("conn1", string.Format(SqlServerNotificationService.INSERT_SCRIPT, "table1"), Arg.Any<object>());
            Assert.AreEqual(1, model.EndpointId);
            Assert.AreEqual("ep1", model.EndpointName);
            Assert.IsTrue(model.IsUp);
            Assert.AreEqual(5, model.RecordedDowntimeMinutes);
        }

        [TestMethod]
        public void NotifyDownShouldSaveRecord()
        {
            SqlServerNotificationService.DbModel model = new();
            _sqlConnectionWrapper.Execute(Arg.Any<string>(), Arg.Any<string>(), Arg.Do<object>(o => model = (SqlServerNotificationService.DbModel)o));

            _service.NotifyDown(new Endpoint { Id = 1, Enabled = true, Name = "ep1", Kind = Endpoint.EndpointKind.Icmp }, DateTimeOffset.Now, new Exception("Test Error Message"));

            _sqlConnectionWrapper.Received(1).Execute("conn1", string.Format(SqlServerNotificationService.INSERT_SCRIPT, "table1"), Arg.Any<object>());
            Assert.AreEqual(1, model.EndpointId);
            Assert.AreEqual("ep1", model.EndpointName);
            Assert.IsFalse(model.IsUp);
            StringAssert.Contains(model.JsonMetadata, @"""downReason"":""Test Error Message""");
        }

        [TestMethod]
        public void ShouldCreateTable()
        {
            _sqlConnectionWrapper.Execute("", "", null)
                .ReturnsForAnyArgs(x => { throw new Exception("ex1"); }, x => 1);

            _service.NotifyMisc(new Endpoint { Id = 1, Enabled = true, Name = "ep1", Kind = Endpoint.EndpointKind.Icmp }, NotificationType.EndpointAdded);

            _sqlConnectionWrapper.ReceivedWithAnyArgs(3).Execute("", "", null);
            Received.InOrder(() =>
            {
                _sqlConnectionWrapper.Execute("conn1", string.Format(SqlServerNotificationService.INSERT_SCRIPT, "table1"), Arg.Any<object>());
                _sqlConnectionWrapper.Execute("conn1", string.Format(SqlServerNotificationService.CREATE_TABLE_SCRIPT, "table1"), Arg.Any<object>());
                _sqlConnectionWrapper.Execute("conn1", string.Format(SqlServerNotificationService.INSERT_SCRIPT, "table1"), Arg.Any<object>());
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

            _sqlConnectionWrapper.DidNotReceiveWithAnyArgs().Execute("", "", null);
        }
    }
}
