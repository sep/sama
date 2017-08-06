using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using sama;
using sama.Models;
using sama.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace TestSama.Services
{
    [TestClass]
    public class UserManagementServiceTests
    {
        UserManagementService _service;
        private ILogger<UserManagementService> _logger;
        private IServiceProvider _provider;
        private ApplicationDbContext _testDbContext;

        private ApplicationUser _testUser;

        [TestInitialize]
        public void Setup()
        {
            _provider = TestUtility.InitDI();
            _testDbContext = new ApplicationDbContext(_provider.GetRequiredService<DbContextOptions<ApplicationDbContext>>());
            _logger = Substitute.For<ILogger<UserManagementService>>();
            _service = new UserManagementService(_logger, _provider.GetRequiredService<DbContextOptions<ApplicationDbContext>>());

            _testUser = new ApplicationUser { UserName = "uSeR", PasswordHash = "b", PasswordHashMetadata = "{}" };
            _testDbContext.Users.Add(_testUser);
            _testDbContext.SaveChanges();
        }

        [TestCleanup]
        public void Teardown()
        {
            _service.Dispose();
        }

        [TestMethod]
        public async Task HasAccountsShouldReturnWhetherAccountsExist()
        {
            Assert.IsTrue(await _service.HasAccounts());

            _testDbContext.Users.Remove(await _testDbContext.Users.FirstAsync());
            _testDbContext.SaveChanges();

            Assert.IsFalse(await _service.HasAccounts());
        }

        [TestMethod]
        public async Task FindUserByUsernameShouldFindUsersCaseInsensitivelyAndTrimmed()
        {
            Assert.IsNotNull(await _service.FindUserByUsername(" user "));
            Assert.IsNotNull(await _service.FindUserByUsername("USER"));
            Assert.IsNull(await _service.FindUserByUsername("USE"));
            Assert.IsNull(await _service.FindUserByUsername("USERR"));
        }

        [TestMethod]
        public async Task GetUserIdAsyncShouldReturnStringRepresentationOfGuid()
        {
            Assert.AreEqual(_testUser.Id.ToString("D"), await _service.GetUserIdAsync(_testUser, CancellationToken.None));
        }

        [TestMethod]
        public async Task GetUserNameAsyncShouldReturnUserName()
        {
            Assert.AreEqual(_testUser.UserName, await _service.GetUserNameAsync(_testUser, CancellationToken.None));
        }

        [TestMethod]
        public void ShouldValidateCredentials()
        {
            var user = new ApplicationUser
            {
                PasswordHash = "Vw9/Szqc/x6w+MH084Mp65G6KnTert22FFZM6kW0uIe5jJbb9U9vD2bRxwN+newi4LgtCuNstnfpCYBqaiEjKQ==",
                PasswordHashMetadata = "{\"HashType\":\"Argon2d\",\"DegreeOfParallelism\":2,\"MemorySize\":65536,\"Iterations\":10,\"Salt\":\"XQnHL1jUIz9OeohxfqKhlI+dG2OQKKyjFDArAewN8y4=\"}"
            };

            Assert.IsTrue(_service.ValidateCredentials(user, "aPassword"));
            Assert.IsFalse(_service.ValidateCredentials(user, "wrongPassword"));
        }

        [TestMethod]
        public async Task CreateInitialShouldNotCreateUserIfOtherUsersExist()
        {
            Assert.IsNull(await _service.CreateInitial("aUser", "aPassword"));
        }

        [TestMethod]
        public async Task ShouldCreateInitialUserAndValidate()
        {
            _testDbContext.Users.Remove(await _testDbContext.Users.FirstAsync());
            _testDbContext.SaveChanges();

            var result = await _service.CreateInitial("aUser", "aPassword");

            Assert.IsNotNull(result);

            Assert.IsTrue(_service.ValidateCredentials(result, "aPassword"));
        }

        [TestMethod]
        public async Task ShouldCreateUserAndValidate()
        {
            var result = await _service.Create("aUser", "pw");

            Assert.IsNotNull(result);

            Assert.IsTrue(_service.ValidateCredentials(result, "pw"));
        }

        [TestMethod]
        public async Task ShouldListUsers()
        {
            Assert.AreEqual(1, (await _service.ListUsers()).Count);
        }

        [TestMethod]
        public async Task ShouldFindUserById()
        {
            Assert.IsNull(await _service.FindByIdAsync("wrongId", CancellationToken.None));

            Assert.AreEqual(_testUser.Id, (await _service.FindByIdAsync(_testUser.Id.ToString("D"), CancellationToken.None)).Id);
        }

        [TestMethod]
        public async Task ShouldResetUserPassword()
        {
            await _service.ResetUserPassword(_testUser.Id, "newPassword");

            using(var newContext = new ApplicationDbContext(_provider.GetRequiredService<DbContextOptions<ApplicationDbContext>>()))
                Assert.AreNotEqual(_testUser.PasswordHash, (await newContext.Users.FirstAsync()).PasswordHash);
        }

        [TestMethod]
        public async Task ShouldDeleteUser()
        {
            Assert.AreEqual(Microsoft.AspNetCore.Identity.IdentityResult.Success, await _service.DeleteAsync(_testUser, CancellationToken.None));

            Assert.AreEqual(0, await _testDbContext.Users.CountAsync());
        }
    }
}
