using FeelShare.Web.Models;
using Microsoft.AspNetCore.Identity;

namespace FeelShare.Web.Data
{
    public static class Seed
    {
        public static async Task AdminAsync(IServiceProvider sp)
        {
            using var scope = sp.CreateScope();
            var roleMgr = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            const string role = "Admin";
            const string email = "admin@feelshare.app"; 
            const string pass = "Admin#12345";       

            if (!await roleMgr.RoleExistsAsync(role))
                await roleMgr.CreateAsync(new IdentityRole(role));

            var user = await userMgr.FindByEmailAsync(email);
            if (user == null)
            {
                user = new ApplicationUser { Email = email, UserName = email, EmailConfirmed = true };
                await userMgr.CreateAsync(user, pass);
                await userMgr.AddToRoleAsync(user, role);
            }
            else if (!await userMgr.IsInRoleAsync(user, role))
            {
                await userMgr.AddToRoleAsync(user, role);
            }
        }
    }
}