using System.Data;
using Dapper;
using MyKicksBuddy.Models.Entities;

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

    
}