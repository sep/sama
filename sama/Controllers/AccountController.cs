using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using sama.Models;
using sama.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace sama.Controllers
{
    [Authorize]
    [ResponseCache(NoStore = true)]
    public class AccountController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManagementService _userService;
        private readonly UserManager<ApplicationUser> _userManager;

        public AccountController(SignInManager<ApplicationUser> signInManager, UserManagementService userService, UserManager<ApplicationUser> userManager)
        {
            _signInManager = signInManager;
            _userService = userService;
            _userManager = userManager;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Login(string returnUrl = null)
        {
            // Clear the existing external cookie to ensure a clean login process
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel vm, string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (ModelState.IsValid)
            {
                var user = await _userService.FindUserByUsername(vm.Username);
                if (user != null && _userService.ValidateCredentials(user, vm.Password))
                {
                    await _signInManager.SignInAsync(user, false);
                    return (string.IsNullOrWhiteSpace(returnUrl) ? RedirectToAction(nameof(EndpointsController.List), "Endpoints") : RedirectToLocal(returnUrl));
                }
            }

            // If we got this far, something failed, redisplay form
            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction(nameof(EndpointsController.IndexRedirect), "Endpoints");
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult CreateInitial()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateInitial(RegisterViewModel vm)
        {
            if (ModelState.IsValid)
            {
                var user = await _userService.CreateInitial(vm.Username, vm.Password);
                if (user != null)
                {
                    await _signInManager.SignInAsync(user, false);
                    return RedirectToAction(nameof(EndpointsController.List), "Endpoints");
                }
                else
                {
                    return RedirectToLocal(null);
                }
            }
            else
            {
                return View(vm);
            }
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(RegisterViewModel vm)
        {
            if (ModelState.IsValid)
            {
                var user = await _userService.Create(vm.Username, vm.Password);
                if (user != null)
                {
                    await _signInManager.SignInAsync(user, false);
                    return RedirectToAction(nameof(EndpointsController.List), "Endpoints");
                }
                else
                {
                    return RedirectToLocal(null);
                }
            }
            else
            {
                return View(vm);
            }
        }

        [HttpGet]
        public async Task<IActionResult> List()
        {
            return View(await _userService.ListUsers());
        }

        [HttpGet]
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
            {
                return RedirectToAction(nameof(Edit), new { id = _userManager.GetUserId(User) });
            }

            var user = await _userService.FindByIdAsync(id.Value.ToString("D"), CancellationToken.None);
            if (user == null)
            {
                return NotFound();
            }

            return View(new ResetPasswordViewModel { UserId = id.Value, UserName = user.UserName });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind("UserId,Password,ConfirmPassword")] ResetPasswordViewModel vm)
        {
            if (id != vm.UserId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                await _userService.ResetUserPassword(id, vm.Password);
                return RedirectToAction(nameof(List));
            }
            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null) return NotFound();

            var user = await _userService.FindByIdAsync(id.Value.ToString("D"), CancellationToken.None);
            if (user == null) return NotFound();

            return View(user);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var user = await _userService.FindByIdAsync(id.ToString("D"), CancellationToken.None);
            if (user == null) return NotFound();

            var allUsers = await _userService.ListUsers();
            if (allUsers == null || allUsers.Count < 2)
            {
                ModelState.AddModelError(string.Empty, "The last system user cannot be deleted.");
                return View(user);
            }

            await _userService.DeleteAsync(user, CancellationToken.None);

            return RedirectToAction(nameof(List));
        }

        private IActionResult RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            else
            {
                return RedirectToAction(nameof(EndpointsController.IndexRedirect), "Endpoints");
            }
        }
    }
}
