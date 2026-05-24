using FondoXYZ.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace FondoXYZ.Tests.Helpers
{
    /// <summary>
    /// Fábrica de mocks para UserManager y SignInManager de ASP.NET Core Identity.
    /// Encapsula el boilerplate necesario para que los constructores de Identity no fallen.
    /// </summary>
    public static class IdentityMockHelper
    {
        public static Mock<UserManager<Usuario>> CreateUserManagerMock()
        {
            var store = new Mock<IUserStore<Usuario>>();
            return new Mock<UserManager<Usuario>>(
                store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        }

        public static Mock<SignInManager<Usuario>> CreateSignInManagerMock(
            Mock<UserManager<Usuario>> userManagerMock)
        {
            var contextAccessor = new Mock<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
            var claimsFactory   = new Mock<IUserClaimsPrincipalFactory<Usuario>>();

            return new Mock<SignInManager<Usuario>>(
                userManagerMock.Object,
                contextAccessor.Object,
                claimsFactory.Object,
                null!, null!, null!, null!);
        }
    }
}
