using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Moq;
using SkyScan.Application.Account.ConfirmEmail;
using SkyScan.Application.Account.ForgotPassword;
using SkyScan.Application.Account.Login;
using SkyScan.Application.Account.Register;
using SkyScan.Core.Repositories_Interfaces;
using SkyScan.Infrastructure.Identity;
using SkyScan.Presentation.Controllers;
using SkyScan.Presentation.Models;
using Xunit;

namespace SkyScan.Tests
{
    // Rewritten for Phase 2d: AccountController is now a thin controller over IMediator +
    // IUserRepository (Identity-current-user lookups) + SignInManager (GoogleLogin only).
    // These tests exercise the controller's own branching (ModelState guards, result-to-
    // IActionResult mapping) against a mocked IMediator — the actual business logic these
    // tests used to exercise directly now lives in, and should be tested via, the individual
    // Command/Query Handlers (RegisterCommandHandler, LoginCommandHandler, etc.) in
    // SkyScan.Application. See Phase 2d report §5.2 Flag F3.
    public class AccountControllerTests
    {
        private readonly Mock<IMediator> _mockMediator;
        private readonly Mock<IUserRepository> _mockUserRepo;
        private readonly Mock<SignInManager<ApplicationUser>> _mockSignInManager;
        private readonly AccountController _controller;

        public AccountControllerTests()
        {
            _mockMediator = new Mock<IMediator>();
            _mockUserRepo = new Mock<IUserRepository>();

            // Mocking SignInManager (still a direct controller dependency for GoogleLogin only)
            var store = new Mock<IUserStore<ApplicationUser>>();
            var mockUserManager = new Mock<UserManager<ApplicationUser>>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
            var contextAccessor = new Mock<IHttpContextAccessor>();
            var claimsFactory = new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>();
            _mockSignInManager = new Mock<SignInManager<ApplicationUser>>(
                mockUserManager.Object,
                contextAccessor.Object,
                claimsFactory.Object,
                null!, null!, null!, null!);

            _controller = new AccountController(_mockMediator.Object, _mockUserRepo.Object, _mockSignInManager.Object);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };

            // Mocking Url helper
            var mockUrlHelper = new Mock<IUrlHelper>();
            mockUrlHelper.Setup(x => x.Action(It.IsAny<UrlActionContext>())).Returns("callbackUrl");
            _controller.Url = mockUrlHelper.Object;
        }

        [Fact]
        public async Task Register_ReturnsView_WhenModelStateIsInvalid()
        {
            // Arrange
            _controller.ModelState.AddModelError("Email", "Required");
            var model = new RegisterViewModel();

            // Act
            var result = await _controller.Register(model);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(model, viewResult.Model);
        }

        [Fact]
        public async Task Register_RedirectsToConfirmation_WhenSuccessful()
        {
            // Arrange
            var model = new RegisterViewModel { Email = "test@example.com", Password = "Password123!", Name = "Test User" };
            _mockMediator.Setup(m => m.Send(It.IsAny<RegisterCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RegisterResult { Success = true });

            // Act
            var result = await _controller.Register(model);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("RegisterConfirmation", redirectResult.ActionName);
            _mockMediator.Verify(m => m.Send(
                It.Is<RegisterCommand>(c => c.Email == model.Email && c.Name == model.Name),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Login_ReturnsView_WhenModelStateIsInvalid()
        {
            // Arrange
            _controller.ModelState.AddModelError("Password", "Required");
            var model = new LoginViewModel();

            // Act
            var result = await _controller.Login(model);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(model, viewResult.Model);
        }

        [Fact]
        public async Task Login_RedirectsToHome_WhenSucceeded()
        {
            // Arrange
            var model = new LoginViewModel { Email = "test@example.com", Password = "Password123!" };
            _mockMediator.Setup(m => m.Send(It.IsAny<LoginCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LoginResult { Succeeded = true });

            // Act
            var result = await _controller.Login(model);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirectResult.ActionName);
            Assert.Equal("Home", redirectResult.ControllerName);
        }

        [Fact]
        public async Task ConfirmEmail_ReturnsSuccessView_WhenSuccessful()
        {
            // Arrange
            string userId = Guid.NewGuid().ToString();
            string token = "token123";
            _mockMediator.Setup(m => m.Send(
                    It.Is<ConfirmEmailCommand>(c => c.UserId == userId && c.Token == token),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ConfirmEmailResult { Succeeded = true });

            // Act
            var result = await _controller.ConfirmEmail(userId, token);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal("ConfirmEmailSuccess", viewResult.ViewName);
        }

        [Fact]
        public async Task ForgotPassword_RedirectsToConfirmation_Always()
        {
            // Arrange
            var model = new ForgotPasswordViewModel { Email = "test@example.com" };
            _mockMediator.Setup(m => m.Send(It.IsAny<ForgotPasswordCommand>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.ForgotPassword(model);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("ForgotPasswordConfirmation", redirectResult.ActionName);
            _mockMediator.Verify(m => m.Send(
                It.Is<ForgotPasswordCommand>(c => c.Email == model.Email),
                It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
