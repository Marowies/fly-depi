using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Moq;
using SkyScan.Core.Entities;
using SkyScan.Core.Repositories_Interfaces;
using SkyScan.Presentation.Controllers;
using SkyScan.Presentation.Models;
using System.Text.Encodings.Web;
using Xunit;

namespace SkyScan.Tests
{
    public class AccountControllerTests
    {
        private readonly Mock<IUserRepository> _mockUserRepo;
        private readonly Mock<IEmailService> _mockEmailService;
        private readonly Mock<UrlEncoder> _mockUrlEncoder;
        private readonly Mock<UserManager<User>> _mockUserManager;
        private readonly Mock<SignInManager<User>> _mockSignInManager;
        private readonly AccountController _controller;

        public AccountControllerTests()
        {
            _mockUserRepo = new Mock<IUserRepository>();
            _mockEmailService = new Mock<IEmailService>();
            _mockUrlEncoder = new Mock<UrlEncoder>();
            
            // Mocking UserManager
            var store = new Mock<IUserStore<User>>();
            _mockUserManager = new Mock<UserManager<User>>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

            // Mocking SignInManager
            var contextAccessor = new Mock<IHttpContextAccessor>();
            var claimsFactory = new Mock<IUserClaimsPrincipalFactory<User>>();
            _mockSignInManager = new Mock<SignInManager<User>>(
                _mockUserManager.Object,
                contextAccessor.Object,
                claimsFactory.Object,
                null!, null!, null!, null!);

            _controller = new AccountController(
                _mockUserRepo.Object,
                _mockEmailService.Object,
                _mockUrlEncoder.Object,
                _mockUserManager.Object,
                _mockSignInManager.Object);

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
            _mockUserRepo.Setup(r => r.RegisterUserAsync(It.IsAny<User>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Success);
            _mockUserRepo.Setup(r => r.GenerateEmailConfirmationTokenAsync(It.IsAny<User>()))
                .ReturnsAsync("token");

            // Act
            var result = await _controller.Register(model);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("RegisterConfirmation", redirectResult.ActionName);
            _mockEmailService.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
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
            _mockUserRepo.Setup(r => r.LoginUserAsync(model.Email, model.Password, model.RememberMe))
                .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);

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
            string userId = "user123";
            string token = "token123";
            var user = new User { Id = Guid.NewGuid() };
            _mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);
            _mockUserRepo.Setup(r => r.ConfirmEmailAsync(user, token)).ReturnsAsync(IdentityResult.Success);

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
            var user = new User { Email = model.Email, Name = "Test" };
            _mockUserRepo.Setup(r => r.GetUserByEmailAsync(model.Email)).ReturnsAsync(user);
            _mockUserRepo.Setup(r => r.GeneratePasswordResetTokenAsync(user)).ReturnsAsync("token");

            // Act
            var result = await _controller.ForgotPassword(model);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("ForgotPasswordConfirmation", redirectResult.ActionName);
            _mockEmailService.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }
    }
}
