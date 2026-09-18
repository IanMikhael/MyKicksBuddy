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
                   created_at AS CreatedAt
            FROM customer_addresses
            WHERE user_id = @UserId
            ORDER BY is_default DESC, created_at DESC";

        return await _db.QueryAsync<CustomerAddress>(sql, new { UserId = userId });
    }

    public async Task<CustomerAddress?> GetByIdAsync(long id)
    {
        const string sql = @"
            SELECT id AS Id, user_id AS UserId, label AS Label,
                   full_address AS FullAddress, latitude AS Latitude,
                   longitude AS Longitude, distance_km AS DistanceKm,
                   is_within_radius AS IsWithinRadius, is_default AS IsDefault,
                   created_at AS CreatedAt
            FROM customer_addresses
            WHERE id = @Id";

        return await _db.QueryFirstOrDefaultAsync<CustomerAddress>(sql, new { Id = id });
    }

    public async Task<long> CreateAsync(CustomerAddress address)
    {
        if (address.IsDefault)
        {
            const string resetSql = @"
                UPDATE customer_addresses 
                SET is_default = 0 
                WHERE user_id = @UserId";

            await _db.ExecuteAsync(resetSql, new { UserId = address.UserId });
        }

        const string sql = @"
            INSERT INTO customer_addresses 
                (user_id, label, full_address, latitude, longitude, 
                distance_km, is_within_radius, is_default)
            VALUES 
                (@UserId, @Label, @FullAddress, @Latitude, @Longitude,
                @DistanceKm, @IsWithinRadius, @IsDefault);
            SELECT LAST_INSERT_ID();";

        try
        {
            return await _db.ExecuteScalarAsync<long>(sql, address);
        }
        catch (Exception ex)
        {
            throw new Exception("Gagal menyimpan alamat.", ex);
        }
    }

    public async Task DeleteAsync(long id, long userId)
    {
        const string sql = @"
            DELETE FROM customer_addresses 
            WHERE id = @Id AND user_id = @UserId";

        await _db.ExecuteAsync(sql, new { Id = id, UserId = userId });
    }
}