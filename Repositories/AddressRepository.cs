using System.Data;
using Dapper;
using MyKicksBuddy.Models.Entities;

namespace MyKicksBuddy.Repositories;

public class AddressRepository : IAddressRepository
{
    private readonly IDbConnection _db;

    public AddressRepository(IDbConnection db)
    {
        _db = db;
    }

    public async Task<IEnumerable<CustomerAddress>> GetByUserIdAsync(long userId)
    {
        const string sql = @"
            SELECT id AS Id, user_id AS UserId, label AS Label,
                   full_address AS FullAddress, latitude AS Latitude,
                   longitude AS Longitude, distance_km AS DistanceKm,
                   is_within_radius AS IsWithinRadius, is_default AS IsDefault,
                   is_active AS IsActive,
                   created_at AS CreatedAt
            FROM customer_addresses
            WHERE user_id = @UserId AND is_active = 1
            ORDER BY is_default DESC, created_at DESC";

        return await _db.QueryAsync<CustomerAddress>(sql, new { UserId = userId });
    }

    public async Task<CustomerAddress?> GetByIdAsync(long id, long userId)
    {
        const string sql = @"
            SELECT id AS Id, user_id AS UserId, label AS Label,
                   full_address AS FullAddress, latitude AS Latitude,
                   longitude AS Longitude, distance_km AS DistanceKm,
                   is_within_radius AS IsWithinRadius, is_default AS IsDefault,
                   is_active AS IsActive,
                   created_at AS CreatedAt
            FROM customer_addresses
            WHERE id = @Id AND user_id = @UserId AND is_active = 1";

        return await _db.QueryFirstOrDefaultAsync<CustomerAddress>(sql, new { Id = id, UserId = userId });
    }

    public async Task<long> CreateAsync(CustomerAddress address)
    {
        if (_db.State != ConnectionState.Open) _db.Open();
        using var transaction = _db.BeginTransaction();
        try
        {
            var existingCount = await _db.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM customer_addresses WHERE user_id = @UserId AND is_active = 1",
                new { address.UserId }, transaction);
            address.IsDefault = address.IsDefault || existingCount == 0;

            if (address.IsDefault)
            {
                await _db.ExecuteAsync(
                    "UPDATE customer_addresses SET is_default = 0 WHERE user_id = @UserId AND is_active = 1",
                    new { address.UserId }, transaction);
            }

            const string sql = @"
                INSERT INTO customer_addresses
                    (user_id, label, full_address, latitude, longitude,
                     distance_km, is_within_radius, is_default, is_active)
                VALUES
                    (@UserId, @Label, @FullAddress, @Latitude, @Longitude,
                     @DistanceKm, @IsWithinRadius, @IsDefault, 1);
                SELECT LAST_INSERT_ID();";

            var id = await _db.ExecuteScalarAsync<long>(sql, address, transaction);
            transaction.Commit();
            return id;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<bool> UpdateAsync(CustomerAddress address)
    {
        const string sql = @"
            UPDATE customer_addresses
            SET label = @Label, full_address = @FullAddress,
                latitude = @Latitude, longitude = @Longitude,
                distance_km = @DistanceKm, is_within_radius = @IsWithinRadius
            WHERE id = @Id AND user_id = @UserId AND is_active = 1";

        if (_db.State != ConnectionState.Open) _db.Open();
        using var transaction = _db.BeginTransaction();
        try
        {
            var affected = await _db.ExecuteAsync(sql, address, transaction);
            if (affected > 0 && address.IsDefault)
            {
                await _db.ExecuteAsync(
                    "UPDATE customer_addresses SET is_default = (id = @Id) WHERE user_id = @UserId AND is_active = 1",
                    new { address.Id, address.UserId }, transaction);
            }

            transaction.Commit();
            return affected > 0;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<bool> DeleteAsync(long id, long userId)
    {
        if (_db.State != ConnectionState.Open) _db.Open();
        using var transaction = _db.BeginTransaction();
        try
        {
            var wasDefault = await _db.ExecuteScalarAsync<bool?>(
                "SELECT is_default FROM customer_addresses WHERE id = @Id AND user_id = @UserId AND is_active = 1",
                new { Id = id, UserId = userId }, transaction);
            if (!wasDefault.HasValue)
                return false;

            var affected = await _db.ExecuteAsync(
                "UPDATE customer_addresses SET is_active = 0, is_default = 0 WHERE id = @Id AND user_id = @UserId AND is_active = 1",
                new { Id = id, UserId = userId }, transaction);

            if (affected > 0 && wasDefault.Value)
            {
                await _db.ExecuteAsync(@"
                    UPDATE customer_addresses
                    SET is_default = 1
                    WHERE id = (
                        SELECT id FROM (
                            SELECT id FROM customer_addresses
                            WHERE user_id = @UserId AND is_active = 1
                            ORDER BY created_at DESC, id DESC LIMIT 1
                        ) candidate
                    )", new { UserId = userId }, transaction);
            }

            transaction.Commit();
            return affected > 0;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<bool> SetDefaultAsync(long id, long userId)
    {
        if (_db.State != ConnectionState.Open) _db.Open();
        using var transaction = _db.BeginTransaction();
        try
        {
            var ownsAddress = await _db.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM customer_addresses WHERE id = @Id AND user_id = @UserId AND is_active = 1",
                new { Id = id, UserId = userId }, transaction) > 0;
            if (!ownsAddress)
                return false;

            await _db.ExecuteAsync(
                "UPDATE customer_addresses SET is_default = (id = @Id) WHERE user_id = @UserId AND is_active = 1",
                new { Id = id, UserId = userId }, transaction);

            transaction.Commit();
            return true;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}
