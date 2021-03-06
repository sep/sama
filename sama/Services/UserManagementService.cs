﻿using Konscious.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using sama.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace sama.Services
{
    public class UserManagementService : IUserStore<ApplicationUser>, IRoleStore<IdentityRole>, IDisposable
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

        public virtual async Task<bool> HasAccounts()
        {
            using(var dbContext = new ApplicationDbContext(_dbContextOptions))
            {
                return await dbContext.Users.AsQueryable().AnyAsync();
            }
        }

        public virtual async Task<ApplicationUser?> FindUserByUsername(string username)
        {
            using (var dbContext = new ApplicationDbContext(_dbContextOptions))
            {
                return await dbContext.Users.AsAsyncEnumerable().FirstOrDefaultAsync(u => u.UserName?.ToLowerInvariant() == username.Trim().ToLowerInvariant());
            }
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
            using (var dbContext = new ApplicationDbContext(_dbContextOptions))
            {
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
        }

        public virtual async Task<ApplicationUser> Create(string username, string password)
        {
            using (var dbContext = new ApplicationDbContext(_dbContextOptions))
            {
                CreatePasswordHash(password, out string hash, out string metadata);
                var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = username.Trim(), PasswordHash = hash, PasswordHashMetadata = metadata };
                dbContext.Users.Add(user);
                await dbContext.SaveChangesAsync();
                return user;
            }
        }

        public virtual async Task<List<ApplicationUser>> ListUsers()
        {
            using (var dbContext = new ApplicationDbContext(_dbContextOptions))
            {
                return await dbContext.Users.AsQueryable().ToListAsync();
            }
        }

        public virtual async Task<ApplicationUser> FindByIdAsync(string userId, CancellationToken cancellationToken)
        {
            using (var dbContext = new ApplicationDbContext(_dbContextOptions))
            {
                return await dbContext.Users.AsAsyncEnumerable().FirstOrDefaultAsync(u => u.Id.ToString("D") == userId) ?? throw new ArgumentException("User does not exist", nameof(userId));
            }
        }

        public virtual async Task ResetUserPassword(Guid id, string password)
        {
            using (var dbContext = new ApplicationDbContext(_dbContextOptions))
            {
                var user = await dbContext.Users.AsQueryable().FirstAsync(u => u.Id == id);
                CreatePasswordHash(password, out string hash, out string metadata);
                user.PasswordHash = hash;
                user.PasswordHashMetadata = metadata;
                dbContext.Update(user);
                await dbContext.SaveChangesAsync();
            }
        }

        public virtual async Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            using (var dbContext = new ApplicationDbContext(_dbContextOptions))
            {
                dbContext.Users.Remove(user);
                await dbContext.SaveChangesAsync();

                return IdentityResult.Success;
            }
        }

        private bool VerifyPasswordHash(string password, string storedHash, string metadata)
        {
            try
            {
                var obj = JsonConvert.DeserializeObject(metadata);
                if (obj == null) return false;

                dynamic metadataObject = obj;
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

        private void CreatePasswordHash(string password, out string hash, out string metadata)
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
