using Microsoft.AspNetCore.Identity;
using sama.Models;
using System;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using Konscious.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace sama.Services
{
    public class UserManagementService : IUserStore<ApplicationUser>, IRoleStore<IdentityRole>
    {
        private readonly ILogger<UserManagementService> _logger;
        private readonly DbContextOptions<ApplicationDbContext> _dbContextOptions;

        public UserManagementService(ILogger<UserManagementService> logger, DbContextOptions<ApplicationDbContext> dbContextOptions)
        {
            _logger = logger;
            _dbContextOptions = dbContextOptions;
        }

        public void Dispose()
        {
        }

        public async Task<bool> HasAccounts()
        {
            using(var dbContext = new ApplicationDbContext(_dbContextOptions))
            {
                return await dbContext.Users.AnyAsync();
            }
        }

        public async Task<ApplicationUser> FindUserByUsername(string username)
        {
            using (var dbContext = new ApplicationDbContext(_dbContextOptions))
            {
                return await dbContext.Users.FirstOrDefaultAsync(u => u.UserName.ToLowerInvariant() == username.Trim().ToLowerInvariant());
            }
        }

        public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            return Task.FromResult(":LOCAL:" + user.Id.ToString("B"));
        }

        public Task<string> GetUserNameAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            return Task.FromResult(user.UserName);
        }

        public bool ValidateCredentials(ApplicationUser user, string password)
        {
            return VerifyPasswordHash(password, user.PasswordHash, user.PasswordHashMetadata);
        }

        public async Task<ApplicationUser> CreateInitial(string username, string password)
        {
            using (var dbContext = new ApplicationDbContext(_dbContextOptions))
            {
                if (!await dbContext.Users.AnyAsync())
                {
                    if (CreatePasswordHash(password, out string hash, out string metadata))
                    {
                        var user = new ApplicationUser { UserName = username.Trim(), PasswordHash = hash, PasswordHashMetadata = metadata };
                        dbContext.Users.Add(user);
                        await dbContext.SaveChangesAsync();
                        return user;
                    }
                }

                return null;
            }
        }

        public async Task<List<ApplicationUser>> ListUsers()
        {
            using (var dbContext = new ApplicationDbContext(_dbContextOptions))
            {
                return await dbContext.Users.ToListAsync();
            }
        }

        public async Task<ApplicationUser> FindByIdAsync(string userId, CancellationToken cancellationToken)
        {
            using (var dbContext = new ApplicationDbContext(_dbContextOptions))
            {
                return await dbContext.Users.FirstOrDefaultAsync(u => u.Id.ToString("B") == userId);
            }
        }

        private bool VerifyPasswordHash(string password, string storedHash, string metadata)
        {
            try
            {
                dynamic metadataObject = JsonConvert.DeserializeObject(metadata);
                if (metadataObject.HashType != "Argon2d") return false;

                var passwordBytes = Encoding.UTF8.GetBytes(password);
                var argon = new Argon2d(passwordBytes)
                {
                    DegreeOfParallelism = metadataObject.DegreeOfParallelism,
                    MemorySize = metadataObject.MemorySize,
                    Iterations = metadataObject.Iterations,
                    Salt = metadataObject.Salt
                };

                var hashBytes = argon.GetBytes(64);
                var storedHashBytes = Convert.FromBase64String(storedHash);
                return CompareSlowly(hashBytes, storedHashBytes);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private bool CreatePasswordHash(string password, out string hash, out string metadata)
        {
            try
            {
                byte[] salt = new byte[32];
                using (var rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(salt);
                }

                var metadataObject = new
                {
                    HashType = "Argon2d",
                    DegreeOfParallelism = 2,
                    MemorySize = 65536,
                    Iterations = 10,
                    Salt = salt
                };
                metadata = JsonConvert.SerializeObject(metadataObject);

                var passwordBytes = Encoding.UTF8.GetBytes(password);
                var argon = new Argon2d(passwordBytes)
                {
                    DegreeOfParallelism = metadataObject.DegreeOfParallelism,
                    MemorySize = metadataObject.MemorySize,
                    Iterations = metadataObject.Iterations,
                    Salt = metadataObject.Salt
                };

                var hashBytes = argon.GetBytes(64);
                hash = Convert.ToBase64String(hashBytes);

                return true;
            }
            catch (Exception)
            {
                hash = null;
                metadata = null;
                return false;
            }
        }

        private bool CompareSlowly(byte[] b1, byte[] b2)
        {
            if (b1 == null || b2 == null) return (b1 == b2);

            uint val = (uint)(b1.Length ^ b2.Length);
            for (var i = 0; i < b1.Length && i < b2.Length; i++)
            {
                val |= (uint)(b1[i] ^ b2[i]);
            }

            return (val == 0);
        }

        #region unused
        public Task<ApplicationUser> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<string> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
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
#endregion
    }
}
