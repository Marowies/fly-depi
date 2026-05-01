using Microsoft.AspNetCore.Identity;
using SkyScan.Core.Entities;
using System.Threading.Tasks;

namespace SkyScan.Core.Repositories_Interfaces
{
    public interface IUserRepository
    {
        Task<IdentityResult> RegisterUserAsync(User user, string password);
        Task<SignInResult> LoginUserAsync(string email, string password, bool rememberMe);
        Task LogoutUserAsync();
        Task<User?> GetUserByEmailAsync(string email);
    }
}
