using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using sama;
using System;

namespace TestSama
{
    public static class TestUtility
    {
        public static IServiceProvider InitDI()
        {
            var collection = new ServiceCollection();
            collection.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseSqlite($"Data Source=testdb_{Guid.NewGuid().ToString("N")}; Cache=Shared; Mode=Memory");
            });

            var provider = collection.BuildServiceProvider();

            var dbContext = provider.GetRequiredService<ApplicationDbContext>();
            dbContext.Database.OpenConnection();
            dbContext.Database.Migrate();
            dbContext.SaveChanges();

            return provider;
        }
    }
}
