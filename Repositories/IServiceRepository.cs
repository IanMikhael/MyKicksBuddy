using MyKicksBuddy.Models.Dtos;

namespace MyKicksBuddy.Repositories;

public interface IServiceRepository
{
    Task<IReadOnlyList<ServiceAdminDto>> GetAllAsync();
    Task<ServiceAdminDto?> GetByIdAsync(long id);
    Task<long> CreateAsync(ServiceAdminFormModel model);
    Task UpdateAsync(ServiceAdminFormModel model);
    Task SetActiveAsync(long id, bool active);
}
