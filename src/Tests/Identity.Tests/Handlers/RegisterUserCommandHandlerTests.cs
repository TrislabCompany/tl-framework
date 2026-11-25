using Trislab.Modules.Identity.Contracts.Services;
using Trislab.Modules.Identity.Contracts.v1.Users.RegisterUser;
using Trislab.Modules.Identity.Features.v1.Users.RegisterUser;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Identity.Tests.Handlers;

/// <summary>
/// Tests for RegisterUserCommandHandler - handles user registration.
/// </summary>
public sealed class RegisterUserCommandHandlerTests
{
    private readonly IUserService _userService;
    private readonly RegisterUserCommandHandler _sut;

    public RegisterUserCommandHandlerTests()
    {
        _userService = Substitute.For<IUserService>();
        _sut = new RegisterUserCommandHandler(_userService);
    }

    #region Handle - Happy Path Tests

    [Fact]
    public async Task Handle_Should_ReturnTheSameAnswer_Without_An_AccountId()
    {
        // Arrange
        var command = new RegisterUserCommand
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@example.com",
            UserName = "johndoe",
            Password = "Password123!",
            ConfirmPassword = "Password123!",
            PhoneNumber = "+1234567890",
            Origin = "web"
        };

        _userService.RegisterAsync(
            command.FirstName,
            command.LastName,
            command.Email,
            command.UserName,
            command.Password,
            command.ConfirmPassword,
            command.PhoneNumber,
            command.Origin,
            Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBe(RegisterUserResponse.CheckYourEmail, "the answer never carries an account id");
    }

    [Fact]
    public async Task Handle_Should_CallUserServiceWithCorrectParameters_When_RegistrationIsRequested()
    {
        // Arrange
        var command = new RegisterUserCommand
        {
            FirstName = "Jane",
            LastName = "Smith",
            Email = "jane.smith@example.com",
            UserName = "janesmith",
            Password = "SecurePass456!",
            ConfirmPassword = "SecurePass456!",
            PhoneNumber = "+9876543210",
            Origin = "mobile"
        };

        _userService.RegisterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert
        await _userService.Received(1).RegisterAsync(
            command.FirstName,
            command.LastName,
            command.Email,
            command.UserName,
            command.Password,
            command.ConfirmPassword,
            command.PhoneNumber,
            command.Origin,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_HandleNullPhoneNumber_When_NotProvided()
    {
        // Arrange
        var command = new RegisterUserCommand
        {
            FirstName = "Test",
            LastName = "User",
            Email = "test@example.com",
            UserName = "testuser",
            Password = "Password123!",
            ConfirmPassword = "Password123!",
            PhoneNumber = null,
            Origin = "web"
        };

        _userService.RegisterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert
        await _userService.Received(1).RegisterAsync(
            command.FirstName,
            command.LastName,
            command.Email,
            command.UserName,
            command.Password,
            command.ConfirmPassword,
            string.Empty, // Should convert null to empty string
            command.Origin,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_HandleNullOrigin_When_NotProvided()
    {
        // Arrange
        var command = new RegisterUserCommand
        {
            FirstName = "Test",
            LastName = "User",
            Email = "test@example.com",
            UserName = "testuser",
            Password = "Password123!",
            ConfirmPassword = "Password123!",
            PhoneNumber = "+1234567890",
            Origin = null
        };

        _userService.RegisterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert
        await _userService.Received(1).RegisterAsync(
            command.FirstName,
            command.LastName,
            command.Email,
            command.UserName,
            command.Password,
            command.ConfirmPassword,
            command.PhoneNumber,
            string.Empty, // Should convert null to empty string
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region Handle - Exception Tests

    [Fact]
    public async Task Handle_Should_ThrowException_When_UserServiceThrows()
    {
        // Arrange
        var command = new RegisterUserCommand
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@example.com",
            UserName = "johndoe",
            Password = "Password123!",
            ConfirmPassword = "Password123!",
            PhoneNumber = "+1234567890",
            Origin = "web"
        };

        var expectedExceptionMessage = "Email already exists";
        _userService.RegisterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException(expectedExceptionMessage));

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await _sut.Handle(command, CancellationToken.None));

        exception.Message.ShouldBe(expectedExceptionMessage);
    }

    #endregion

    #region Handle - Null Command Tests

    [Fact]
    public async Task Handle_Should_ThrowArgumentNullException_When_CommandIsNull()
    {
        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await _sut.Handle(null!, CancellationToken.None));
    }

    #endregion

    #region Handle - CancellationToken Tests

    [Fact]
    public async Task Handle_Should_PassCancellationToken_ToUserService()
    {
        // Arrange
        var command = new RegisterUserCommand
        {
            FirstName = "Test",
            LastName = "User",
            Email = "test@example.com",
            UserName = "testuser",
            Password = "Password123!",
            ConfirmPassword = "Password123!",
            PhoneNumber = "+1234567890",
            Origin = "web"
        };

        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        _userService.RegisterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), cancellationToken)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.Handle(command, cancellationToken);

        // Assert
        await _userService.Received(1).RegisterAsync(
            command.FirstName,
            command.LastName,
            command.Email,
            command.UserName,
            command.Password,
            command.ConfirmPassword,
            command.PhoneNumber,
            command.Origin,
            cancellationToken);
    }

    #endregion

    #region Handle - Edge Cases Tests

    [Fact]
    public async Task Handle_Should_HandleEmptyStrings_When_ProvidedInCommand()
    {
        // Arrange
        var command = new RegisterUserCommand
        {
            FirstName = "",
            LastName = "",
            Email = "test@example.com",
            UserName = "testuser",
            Password = "Password123!",
            ConfirmPassword = "Password123!",
            PhoneNumber = "",
            Origin = ""
        };

        _userService.RegisterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.Status.ShouldBe("check_your_email");
        await _userService.Received(1).RegisterAsync("", "", "test@example.com", "testuser", "Password123!", "Password123!", "", "", Arg.Any<CancellationToken>());
    }

    #endregion
}