using System.Data;
using Dapper;
using MyKicksBuddy.Models.Entities;
using MyKicksBuddy.Models.Dtos;

namespace MyKicksBuddy.Repositories;

public class UserRepository : IUserRepository
{
    private readonly IDbConnection _db;

    public UserRepository(IDbConnection db)
    {
        _db = db;
    }

    public async Task<User?> GetByEmailOrPhoneAsync(string emailOrPhone)
    {
        const string sql = @"
            SELECT id AS Id, role AS Role, full_name AS FullName, email AS Email,
                   phone AS Phone, password_hash AS PasswordHash, is_active AS IsActive,
                   created_at AS CreatedAt, updated_at AS UpdatedAt
            FROM users
            WHERE email = @Value OR phone = @Value
            LIMIT 1";

        return await _db.QueryFirstOrDefaultAsync<User>(sql, new { Value = emailOrPhone });
    }

    public async Task<bool> ExistsByEmailOrPhoneAsync(string? email, string? phone)
    {
        const string sql = @"
            SELECT COUNT(*)
            FROM users
            WHERE (@Email IS NOT NULL AND email = @Email)
               OR (@Phone IS NOT NULL AND phone = @Phone)";

        return await _db.ExecuteScalarAsync<int>(sql, new { Email = email, Phone = phone }) > 0;
    }

    public async Task<User?> GetByIdAsync(long id)
    {
        const string sql = @"
            SELECT id AS Id, role AS Role, full_name AS FullName, email AS Email,
                   phone AS Phone, password_hash AS PasswordHash, is_active AS IsActive,
                   created_at AS CreatedAt, updated_at AS UpdatedAt
            FROM users
            WHERE id = @Id";

        return await _db.QueryFirstOrDefaultAsync<User>(sql, new { Id = id });
    }

    public async Task<long> CreateAsync(User user)
    {
        const string sql = @"
            INSERT INTO users (role, full_name, email, phone, password_hash, is_active)
            VALUES (@Role, @FullName, @Email, @Phone, @PasswordHash, @IsActive);
            SELECT LAST_INSERT_ID();";

        return await _db.ExecuteScalarAsync<long>(sql, user);
    }

    public async Task<IReadOnlyList<InternalUserDto>> GetInternalUsersAsync()
    {
        const string sql = @"SELECT id AS Id, full_name AS FullName, role AS Role, email AS Email,
            phone AS Phone, is_active AS IsActive, created_at AS CreatedAt
            FROM users WHERE role IN ('kasir','admin') ORDER BY role, full_name";
        return (await _db.QueryAsync<InternalUserDto>(sql)).ToList();
    }

    public async Task<bool> UpdateAdminProfileAsync(long id, string fullName, string email, string? phone)
    {
        if (await _db.ExecuteScalarAsync<int>(@"SELECT COUNT(*) FROM users WHERE id<>@Id AND (email=@Email OR (@Phone IS NOT NULL AND phone=@Phone))", new { Id=id, Email=email, Phone=phone }) > 0) return false;
        return await _db.ExecuteAsync(@"UPDATE users SET full_name=@FullName,email=@Email,phone=@Phone,updated_at=CURRENT_TIMESTAMP WHERE id=@Id AND role='admin' AND is_active=1", new { Id=id, FullName=fullName, Email=email, Phone=phone }) == 1;
    }

    public async Task<bool> UpdateAdminPasswordHashAsync(long id, string expectedHash, string newHash) =>
        await _db.ExecuteAsync(@"UPDATE users SET password_hash=@NewHash,updated_at=CURRENT_TIMESTAMP WHERE id=@Id AND role='admin' AND is_active=1 AND password_hash=@ExpectedHash", new { Id=id, ExpectedHash=expectedHash, NewHash=newHash }) == 1;

    
}
