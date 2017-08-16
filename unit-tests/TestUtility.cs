using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using sama;
using System;
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
    }
}
