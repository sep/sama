using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using sama.Controllers;
using sama.Models;
using sama.Services;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TestSama.Controllers
{
    [TestClass]
    public class AccountControllerTests
    {
        private AccountController _controller;
        UserManager<ApplicationUser> _userManager;
        SignInManager<ApplicationUser> _signInManager;
        UserManagementService _userService;
        IServiceProvider _provider;
        IAuthenticationService _authService;

        [TestInitialize]
        public void Setup()
        {
            _userManager = Substitute.For<UserManager<ApplicationUser>>(Substitute.For<IUserStore<ApplicationUser>>(), null, null, null, null, null, null, null, null);
            _signInManager = Substitute.For<SignInManager<ApplicationUser>>(_userManager, Substitute.For<IHttpContextAccessor>(), Substitute.For<IUserClaimsPrincipalFactory<ApplicationUser>>(), null, null, null);
            _userService = Substitute.For<UserManagementService>(null, null);
            _controller = new AccountController(_signInManager, _userService, _userManager);

            _provider = Substitute.For<IServiceProvider>();
            _authService = Substitute.For<IAuthenticationService>();
            _provider.GetService(typeof(IAuthenticationService)).Returns(_authService);

            _controller.ControllerContext = new ControllerContext();
            _controller.ControllerContext.HttpContext = Substitute.For<HttpContext>();
            _controller.ControllerContext.HttpContext.RequestServices.Returns(_provider);

            _controller.TempData = Substitute.For<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataDictionary>();

            _controller.Url = Substitute.For<IUrlHelper>();
        }

        [TestMethod]
        public async Task GetLoginShouldLogOut()
        {
            var result = await _controller.Login();

            Assert.IsInstanceOfType(result, typeof(ViewResult));
            await _authService.Received().SignOutAsync(Arg.Any<HttpContext>(), Arg.Any<string>(), Arg.Any<AuthenticationProperties>());
        }

        [TestMethod]
        public async Task PostLoginShouldLogIn()
        {
            var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "user" };
            _userService.FindUserByUsername("user").Returns(user);
            _userService.ValidateCredentials(user, "pass").Returns(true);

            var result = await _controller.Login(new LoginViewModel { Username = "user", Password = "pass" });

            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            await _signInManager.Received().SignInAsync(user, false, null, true);
        }

        [TestMethod]
        public async Task PostLoginWithWrongCredentialsShouldNotLogIn()
        {
            var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "user" };
            _userService.FindUserByUsername("user").Returns(user);
            _userService.ValidateCredentials(user, "pass").Returns(false);

            var result = await _controller.Login(new LoginViewModel { Username = "user", Password = "pass" });

            Assert.IsInstanceOfType(result, typeof(ViewResult));
            await _signInManager.DidNotReceive().SignInAsync(Arg.Any<ApplicationUser>(), Arg.Any<bool>(), Arg.Any<string>(), Arg.Any<bool>());
        }

        [TestMethod]
        public async Task PostLogoutShouldLogOut()
        {
            var result = await _controller.Logout();

            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            await _signInManager.Received().SignOutAsync();
        }

        [TestMethod]
        public async Task PostCreateInitialShouldCreateUserAndLogIn()
        {
            var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "a" };
            _userService.CreateInitial("a", "b").Returns(user);

            var result = await _controller.CreateInitial(new RegisterViewModel { Username = "a", Password = "b", ConfirmPassword = "b" });

            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            await _signInManager.Received().SignInAsync(user, false, null, true);
        }

        [TestMethod]
        public async Task PostCreateShouldCreateUser()
        {
            var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "a" };
            _userService.Create("a", "b").Returns(user);

            var result = await _controller.Create(new RegisterViewModel { Username = "a", Password = "b", ConfirmPassword = "b" });

            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            await _signInManager.DidNotReceive().SignInAsync(Arg.Any<ApplicationUser>(), Arg.Any<bool>(), Arg.Any<string>(), Arg.Any<bool>());
        }

        [TestMethod]
        public async Task GetListShouldShowUserList()
        {
            var users = new List<ApplicationUser> { new ApplicationUser() };
            _userService.ListUsers().Returns(users);

            var result = await _controller.List();
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            Assert.AreSame(users, (result as ViewResult).Model);
        }

        [TestMethod]
        public async Task GetEditWithoutIdShouldRedirectToCurrentUserEdit()
        {
            _controller.ControllerContext.HttpContext.User.Returns(new System.Security.Claims.ClaimsPrincipal());
            _userManager.GetUserId(Arg.Any<System.Security.Claims.ClaimsPrincipal>()).Returns("userId");
            var result = await _controller.Edit(null);

            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var redirect = result as RedirectToActionResult;
            Assert.AreEqual("Edit", redirect.ActionName);
            Assert.AreEqual("userId", redirect.RouteValues["id"]);
        }

        [TestMethod]
        public async Task GetEditWithIdShouldShowEditView()
        {
            var id = Guid.NewGuid();
            var user = new ApplicationUser { Id = id, UserName = "user" };
            _userService.FindByIdAsync(id.ToString("D"), CancellationToken.None).Returns(user);

            var result = await _controller.Edit(id);

            Assert.IsInstanceOfType(result, typeof(ViewResult));
        }

        [TestMethod]
        public async Task PostEditShouldResetPassword()
        {
            var id = Guid.NewGuid();

            var result = await _controller.Edit(id, new ResetPasswordViewModel { UserId = id, UserName = "a", Password = "b", ConfirmPassword = "b" });

            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            await _userService.Received().ResetUserPassword(id, "b");
        }

        [TestMethod]
        public async Task GetDeleteShouldShowDeleteView()
        {
            var id = Guid.NewGuid();
            var user = new ApplicationUser { Id = id, UserName = "user" };
            _userService.FindByIdAsync(id.ToString("D"), CancellationToken.None).Returns(user);

            var result = await _controller.Delete(id);

            Assert.IsInstanceOfType(result, typeof(ViewResult));
            Assert.AreSame(user, (result as ViewResult).Model);
        }

        [TestMethod]
        public async Task PostDeleteWhenLastUserShouldNotDelete()
        {
            var id = Guid.NewGuid();
            var user = new ApplicationUser { Id = id, UserName = "user" };
            _userService.FindByIdAsync(id.ToString("D"), CancellationToken.None).Returns(user);
            _userService.ListUsers().Returns(new List<ApplicationUser> { user });

            var result = await _controller.DeleteConfirmed(id);

            Assert.IsInstanceOfType(result, typeof(ViewResult));
            Assert.AreEqual("The last system user cannot be deleted.", _controller.ModelState[""].Errors[0].ErrorMessage);
            await _userService.DidNotReceive().DeleteAsync(Arg.Any<ApplicationUser>(), Arg.Any<CancellationToken>());
        }

        [TestMethod]
        public async Task PostDeleteShouldDeleteUser()
        {
            var id = Guid.NewGuid();
            var user = new ApplicationUser { Id = id, UserName = "user" };
            _userService.FindByIdAsync(id.ToString("D"), CancellationToken.None).Returns(user);
            _userService.ListUsers().Returns(new List<ApplicationUser> { user, new ApplicationUser() });

            var result = await _controller.DeleteConfirmed(id);

            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            await _userService.Received().DeleteAsync(user, CancellationToken.None);
        }
    }
}
