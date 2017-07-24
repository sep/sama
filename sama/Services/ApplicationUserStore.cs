using Microsoft.AspNetCore.Identity;
using sama.Models;
using System;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace sama.Services
{
    public class ApplicationUserStore : IUserStore<ApplicationUser>, IRoleStore<IdentityRole>
    {
        private readonly ILogger<ApplicationUserStore> _logger;
        private readonly DbContextOptions<ApplicationDbContext> _dbContextOptions;

        public ApplicationUserStore(ILogger<ApplicationUserStore> logger, DbContextOptions<ApplicationDbContext> dbContextOptions)
        {
            _logger = logger;
            _dbContextOptions = dbContextOptions;
        }

        public Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
        }

        public Task<ApplicationUser> FindByIdAsync(string userId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<ApplicationUser> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
        {
            return Task.FromResult(new ApplicationUser { Id = normalizedUserName, UserName = normalizedUserName, NormalizedUserName = normalizedUserName });
        }

        public Task<string> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            return Task.FromResult(user.Id);
        }

        public Task<string> GetUserNameAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            return Task.FromResult(user.UserName);
        }


        public Task SetNormalizedUserNameAsync(ApplicationUser user, string normalizedName, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task SetUserNameAsync(ApplicationUser user, string userName, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }








        Task<IdentityResult> IRoleStore<IdentityRole>.CreateAsync(IdentityRole role, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        Task<IdentityResult> IRoleStore<IdentityRole>.UpdateAsync(IdentityRole role, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        Task<IdentityResult> IRoleStore<IdentityRole>.DeleteAsync(IdentityRole role, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        Task<string> IRoleStore<IdentityRole>.GetRoleIdAsync(IdentityRole role, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        Task<string> IRoleStore<IdentityRole>.GetRoleNameAsync(IdentityRole role, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        Task IRoleStore<IdentityRole>.SetRoleNameAsync(IdentityRole role, string roleName, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        Task<string> IRoleStore<IdentityRole>.GetNormalizedRoleNameAsync(IdentityRole role, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        Task IRoleStore<IdentityRole>.SetNormalizedRoleNameAsync(IdentityRole role, string normalizedName, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        Task<IdentityRole> IRoleStore<IdentityRole>.FindByIdAsync(string roleId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        Task<IdentityRole> IRoleStore<IdentityRole>.FindByNameAsync(string normalizedRoleName, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
