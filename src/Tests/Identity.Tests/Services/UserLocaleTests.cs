using Finbuckle.MultiTenant.Abstractions;
using Trislab.Framework.Shared.Multitenancy;
using Trislab.Framework.Storage.Services;
using Trislab.Modules.Identity.Contracts.Services;
using Trislab.Modules.Identity.Domain;
using Trislab.Modules.Identity.Services;
using Identity.Tests.Support;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Identity.Tests.Services;

/// <summary>
/// Covers the User.Locale foundation: UpdateAsync persists the locale onto the entity and
/// GetAsync projects it back onto the DTO.
/// </summary>
public sealed class UserLocaleTests
{
    private readonly UserManager<TrislabUser> _userManager;
    private readonly SignInManager<TrislabUser> _signInManager;
    private readonly IStorageService _storageService;
    private readonly ITenantAccessor _tenantAccessor;

    public UserLocaleTests()
    {
        _userManager = Substitute.For<UserManager<TrislabUser>>(
            Substitute.For<IUserStore<TrislabUser>>(), null, null, null, null, null, null, null, null);
        _signInManager = Substitute.For<SignInManager<TrislabUser>>(
            _userManager,
            Substitute.For<IHttpContextAccessor>(),
            Substitute.For<IUserClaimsPrincipalFactory<TrislabUser>>(),
            Options.Create(new IdentityOptions()),
            Substitute.For<ILogger<SignInManager<TrislabUser>>>(),
            Substitute.For<IAuthenticationSchemeProvider>(),
            Substitute.For<IUserConfirmation<TrislabUser>>());
        _signInManager.RefreshSignInAsync(Arg.Any<TrislabUser>()).Returns(Task.CompletedTask);
        _storageService = Substitute.For<IStorageService>();
        _tenantAccessor = Substitute.For<ITenantAccessor>();
    }

    private UserProfileService CreateSut() =>
        new(_userManager, _signInManager, _storageService, _tenantAccessor,
            Substitute.For<IRequestContextService>(), new IdentityErrorDescriber());

    [Fact]
    public async Task UpdateAsync_persists_the_supplied_locale_onto_the_user()
    {
        // Arrange
        var user = new TrislabUser { Id = "u1", Email = "u@codefi.com.br", UserName = "u" };
        _userManager.FindByIdAsync("u1").Returns(user);
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);
        var sut = CreateSut();

        // Act
        await sut.UpdateAsync("u1", "First", "Last", string.Empty, null!, false, "sl-SI", null, CancellationToken.None);

        // Assert
        user.Locale.ShouldBe("sl-SI");
    }

    [Fact]
    public async Task UpdateAsync_with_null_locale_preserves_the_existing_value()
    {
        // Arrange — a text-only edit forwards a null locale; the user already chose en-US.
        var user = new TrislabUser { Id = "u1", Email = "u@codefi.com.br", UserName = "u", Locale = "en-US" };
        _userManager.FindByIdAsync("u1").Returns(user);
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);
        var sut = CreateSut();

        // Act
        await sut.UpdateAsync("u1", "First", "Last", string.Empty, null!, false, null, null, CancellationToken.None);

        // Assert
        user.Locale.ShouldBe("en-US");
    }

    [Fact]
    public async Task GetAsync_projects_the_persisted_locale_onto_the_dto()
    {
        // Arrange
        var user = new TrislabUser { Id = "u1", Email = "u@codefi.com.br", UserName = "u", Locale = "sl-SI" };
        _userManager.Users.Returns(new[] { user }.AsAsyncQueryable());
        var sut = CreateSut();

        // Act
        var dto = await sut.GetAsync("u1", CancellationToken.None);

        // Assert
        dto.Locale.ShouldBe("sl-SI");
    }
}
