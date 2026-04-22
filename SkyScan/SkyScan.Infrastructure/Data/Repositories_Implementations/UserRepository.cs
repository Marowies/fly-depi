using SkyScan.Core.Entities;
using SkyScan.Core.Repositories_Interfaces;
using SkyScan.Infrastructure.Data.Data_Sources;

namespace SkyScan.Infrastructure.Data.Repositories_Implementations
{
    public class UserRepository : GenericRepository<User>, IUserRepository
    {
        public UserRepository(SkyScanDbContext context) : base(context)
        {
        }
    }
}
