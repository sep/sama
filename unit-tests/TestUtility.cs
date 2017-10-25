using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using sama;
using sama.Extensions;
using sama.Models;
using sama.Services;
using System;
using System.Collections.Generic;
using System.Net.Http;

namespace TestSama
{
    public static class TestUtility
    {
        public static IServiceProvider InitDI()
        {
            var collection = new ServiceCollection();

            var sqliteConnection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source=file:testdb_{Guid.NewGuid().ToString("N")}.db?mode=memory");
            sqliteConnection.Open();

            collection.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseSqlite(sqliteConnection);
            });

            collection.AddSingleton<HttpClientHandler>(Substitute.ForPartsOf<TestHttpHandler>());
            collection.AddSingleton(Substitute.For<MonitorJob>(null, null));

            var notifier1 = Substitute.For<INotificationService>();
            collection.AddSingleton(notifier1);
            var notifier2 = Substitute.For<INotificationService>();
            collection.AddSingleton(notifier2);

            var provider = collection.BuildServiceProvider(true);

            using (var scope = provider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                dbContext.Database.OpenConnection();
                dbContext.Database.Migrate();
                dbContext.SaveChanges();
            }

            return provider;
        }

        public static Endpoint CreateHttpEndpoint(string name, bool enabled = true, int id = 0, string httpLocation = null, string httpResponseMatch = null, List<int> httpStatusCodes = null)
        {
            var endpoint = new Endpoint
            {
                Id = id,
                Name = name,
                Enabled = enabled,
                Kind = Endpoint.EndpointKind.Http
            };

            if (httpLocation != null) endpoint.SetHttpLocation(httpLocation);
            if (httpResponseMatch != null) endpoint.SetHttpResponseMatch(httpResponseMatch);
            if (httpStatusCodes != null) endpoint.SetHttpStatusCodes(httpStatusCodes);

            return endpoint;
        }

        public static Endpoint CreateIcmpEndpoint(string name, bool enabled = true, int id = 0, string icmpAddress = null)
        {
            var endpoint = new Endpoint
            {
                Id = id,
                Name = name,
                Enabled = enabled,
                Kind = Endpoint.EndpointKind.Icmp
            };

            if (icmpAddress != null) endpoint.SetIcmpAddress(icmpAddress);

            return endpoint;
        }
    }
}
