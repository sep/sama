using Konscious.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using sama.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace sama.Services
{
    public class UserManagementService(ILogger<UserManagementService> _logger, DbContextOptions<ApplicationDbContext> _dbContextOptions) : IUserStore<ApplicationUser>, IRoleStore<IdentityRole>, IDisposable
    {
        private bool _disposedValue;

        public virtual async Task<bool> HasAccounts()
        {
            using var dbContext = new ApplicationDbContext(_dbContextOptions);
            return await dbContext.Users.AsQueryable().AnyAsync();
        }

        public virtual async Task<ApplicationUser?> FindUserByUsername(string username)
        {
			using var dbContext = new ApplicationDbContext(_dbContextOptions);
			return await dbContext.Users.AsAsyncEnumerable().FirstOrDefaultAsync(u => u.UserName?.ToLowerInvariant() == username.Trim().ToLowerInvariant());
		}

        public virtual Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            return Task.FromResult(user.Id.ToString("D"));
        }

        public virtual Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            return Task.FromResult(user.UserName);
        }

        public virtual bool ValidateCredentials(ApplicationUser user, string password)
        {
            return VerifyPasswordHash(password, user.PasswordHash!, user.PasswordHashMetadata!);
        }

        public virtual async Task<ApplicationUser?> CreateInitial(string username, string password)
        {
            using var dbContext = new ApplicationDbContext(_dbContextOptions);
            if (!await dbContext.Users.AsQueryable().AnyAsync())
            {
                CreatePasswordHash(password, out string hash, out string metadata);
                var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = username.Trim(), PasswordHash = hash, PasswordHashMetadata = metadata };
                dbContext.Users.Add(user);
                await dbContext.SaveChangesAsync();
                return user;
            }

            return null;
        }

        public virtual async Task<ApplicationUser> Create(string username, string password)
        {
            using var dbContext = new ApplicationDbContext(_dbContextOptions);
            CreatePasswordHash(password, out string hash, out string metadata);
            var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = username.Trim(), PasswordHash = hash, PasswordHashMetadata = metadata };
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();
            return user;
        }

        public virtual async Task<List<ApplicationUser>> ListUsers()
        {
            using var dbContext = new ApplicationDbContext(_dbContextOptions);
            return await dbContext.Users.AsQueryable().ToListAsync();
        }

        public virtual async Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken cancellationToken)
        {
            using var dbContext = new ApplicationDbContext(_dbContextOptions);
            return await dbContext.Users.AsAsyncEnumerable().FirstOrDefaultAsync(u => u.Id.ToString("D") == userId, cancellationToken: cancellationToken);
        }

        public virtual async Task ResetUserPassword(Guid id, string password)
        {
            using var dbContext = new ApplicationDbContext(_dbContextOptions);
            var user = await dbContext.Users.AsQueryable().FirstAsync(u => u.Id == id);
            CreatePasswordHash(password, out string hash, out string metadata);
            user.PasswordHash = hash;
            user.PasswordHashMetadata = metadata;
            dbContext.Update(user);
            await dbContext.SaveChangesAsync();
        }

        public virtual async Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            using var dbContext = new ApplicationDbContext(_dbContextOptions);
            dbContext.Users.Remove(user);
            await dbContext.SaveChangesAsync(cancellationToken);

            return IdentityResult.Success;
        }

        private bool VerifyPasswordHash(string password, string storedHash, string metadata)
        {
            try
            {
                var node = JsonNode.Parse(metadata)?.AsObject();
                if (node == null) return false;

                if ((string?)node["HashType"] != "Argon2d") return false;
                var degreeOfParallelism = (int)node["DegreeOfParallelism"]!;
                var memorySize = (int)node["MemorySize"]!;
                var iterations = (int)node["Iterations"]!;
                var salt = Convert.FromBase64String((string)node["Salt"]!);

                var passwordBytes = Encoding.UTF8.GetBytes(password);
                var argon = new Argon2d(passwordBytes)
                {
                    DegreeOfParallelism = degreeOfParallelism,
                    MemorySize = memorySize,
                    Iterations = iterations,
                    Salt = salt,
                };

                var hashBytes = argon.GetBytes(64);
                var storedHashBytes = Convert.FromBase64String(storedHash);
                return CompareSlowly(hashBytes, storedHashBytes);
            }
            catch (Exception)
            {
                _logger.LogWarning("Password hash verification failed!");
                return false;
            }
        }

        private static void CreatePasswordHash(string password, out string hash, out string metadata)
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
                Salt = Convert.ToBase64String(salt),
            };
            metadata = JsonSerializer.Serialize(metadataObject);

            var passwordBytes = Encoding.UTF8.GetBytes(password);
            var argon = new Argon2d(passwordBytes)
            {
                DegreeOfParallelism = metadataObject.DegreeOfParallelism,
                MemorySize = metadataObject.MemorySize,
                Iterations = metadataObject.Iterations,
                Salt = salt,
            };

            var hashBytes = argon.GetBytes(64);
            hash = Convert.ToBase64String(hashBytes);
        }

        private static bool CompareSlowly(byte[] b1, byte[] b2)
        {
            if (b1 == null || b2 == null) return (b1 == b2);

            uint val = (uint)(b1.Length ^ b2.Length);
            for (var i = 0; i < b1.Length && i < b2.Length; i++)
            {
                val |= (uint)(b1[i] ^ b2[i]);
            }

            return (val == 0);
        }

        #region IDisposable pattern
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion

        #region unused
        public Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task SetNormalizedUserNameAsync(ApplicationUser user, string? normalizedName, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task SetUserNameAsync(ApplicationUser user, string? userName, CancellationToken cancellationToken)
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

        Task<string?> IRoleStore<IdentityRole>.GetRoleNameAsync(IdentityRole role, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        Task IRoleStore<IdentityRole>.SetRoleNameAsync(IdentityRole role, string? roleName, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        Task<string?> IRoleStore<IdentityRole>.GetNormalizedRoleNameAsync(IdentityRole role, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        Task IRoleStore<IdentityRole>.SetNormalizedRoleNameAsync(IdentityRole role, string? normalizedName, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        Task<IdentityRole?> IRoleStore<IdentityRole>.FindByIdAsync(string roleId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        Task<IdentityRole?> IRoleStore<IdentityRole>.FindByNameAsync(string normalizedRoleName, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
        #endregion
    }
}
