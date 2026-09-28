using System.Data;
using Dapper;
using MyKicksBuddy.Models.Dtos;

namespace MyKicksBuddy.Repositories;

public sealed class ServiceRepository : IServiceRepository
{
    private readonly IDbConnection _db;
    public ServiceRepository(IDbConnection db) => _db = db;

    public async Task<IReadOnlyList<ServiceAdminDto>> GetAllAsync() => (await _db.QueryAsync<ServiceAdminDto>(@"
        SELECT id AS Id, name AS Name, description AS Description, price AS Price,
               estimated_duration_days AS EstimatedDurationDays, is_active AS IsActive
        FROM services ORDER BY is_active DESC, name ASC")).ToList();

    public Task<ServiceAdminDto?> GetByIdAsync(long id) => _db.QueryFirstOrDefaultAsync<ServiceAdminDto>(@"
        SELECT id AS Id, name AS Name, description AS Description, price AS Price,
               estimated_duration_days AS EstimatedDurationDays, is_active AS IsActive
        FROM services WHERE id=@Id", new { Id = id });

    public Task<long> CreateAsync(ServiceAdminFormModel model) => _db.ExecuteScalarAsync<long>(@"
        INSERT INTO services(name,description,price,estimated_duration_days,is_active)
        VALUES(@Name,@Description,@Price,@EstimatedDurationDays,@IsActive); SELECT LAST_INSERT_ID();", model);

    public Task UpdateAsync(ServiceAdminFormModel model) => _db.ExecuteAsync(@"
        UPDATE services SET name=@Name, description=@Description, price=@Price,
          estimated_duration_days=@EstimatedDurationDays, is_active=@IsActive WHERE id=@Id", model);

    public Task SetActiveAsync(long id, bool active) => _db.ExecuteAsync("UPDATE services SET is_active=@Active WHERE id=@Id", new { Id = id, Active = active });
}
