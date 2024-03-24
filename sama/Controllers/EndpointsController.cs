using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using sama.Extensions;
using sama.Models;
using sama.Services;
using System.Linq;
using System.Threading.Tasks;

namespace sama.Controllers
{
    [Authorize]
    public class EndpointsController(ApplicationDbContext _context, StateService _stateService, UserManagementService _userService, AggregateNotificationService _notifier) : Controller
    {
        [AllowAnonymous]
        public async Task<IActionResult> IndexRedirect()
        {
            if (await _userService.HasAccounts())
            {
                return RedirectToAction(nameof(Index));
            }
            else
            {
                return RedirectToAction(nameof(AccountController.CreateInitial), "Account");
            }
        }

        // GET: Endpoints/Index
        [AllowAnonymous]
        public async Task<IActionResult> Index()
        {
            ViewData.Add("CurrentStates", _stateService.GetAll());

            var endpoints = await _context.Endpoints.AsQueryable().ToListAsync();
            return View(endpoints.Select(e => e.ToEndpointViewModel()));
        }

        // GET: Endpoints/IndexPartial
        [AllowAnonymous]
        public async Task<IActionResult> IndexPartial()
        {
            ViewData.Add("CurrentStates", _stateService.GetAll());

            var endpoints = await _context.Endpoints.AsQueryable().ToListAsync();
            return PartialView("_IndexEndpointsPartial", endpoints.Select(e => e.ToEndpointViewModel()));
        }

        // GET: Endpoints/List
        public async Task<IActionResult> List()
        {
            ViewData.Add("CurrentStates", _stateService.GetAll());

            var endpoints = await _context.Endpoints.AsQueryable().ToListAsync();
            return View(endpoints.Select(e => e.ToEndpointViewModel()));
        }

        // GET: Endpoints/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var endpoint = await _context.Endpoints.AsQueryable()
                .SingleOrDefaultAsync(m => m.Id == id);
            if (endpoint == null)
            {
                return NotFound();
            }

            ViewData.Add("State", _stateService.GetStatus(endpoint.Id));

            return View(endpoint.ToEndpointViewModel());
        }

        // GET: Endpoints/Create
        public IActionResult Create(Endpoint.EndpointKind kind = Endpoint.EndpointKind.Http)
        {
            ViewData["Kind"] = kind;
            ViewData["PostTarget"] = "Create" + kind.ToString();
            return View();
        }

        // POST: Endpoints/CreateHttp
        [HttpPost, ActionName("CreateHttp")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(HttpEndpointViewModel vm)
        {
            if (ModelState.IsValid)
            {
                var endpoint = vm.ToEndpoint();
                endpoint.Id = 0;
                _context.Add(endpoint);
                await _context.SaveChangesAsync();
                NotifyEvent(endpoint, NotificationType.EndpointAdded);
                return RedirectToAction(nameof(List));
            }
            return View(nameof(Create), vm);
        }

        // POST: Endpoints/CreateIcmp
        [HttpPost, ActionName("CreateIcmp")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(IcmpEndpointViewModel vm)
        {
            if (ModelState.IsValid)
            {
                var endpoint = vm.ToEndpoint();
                endpoint.Id = 0;
                _context.Add(endpoint);
                await _context.SaveChangesAsync();
                NotifyEvent(endpoint, NotificationType.EndpointAdded);
                return RedirectToAction(nameof(List));
            }
            return View(nameof(Create), vm);
        }

        // GET: Endpoints/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var endpoint = await _context.Endpoints.AsQueryable().SingleOrDefaultAsync(m => m.Id == id);
            if (endpoint == null)
            {
                return NotFound();
            }

            ViewData["PostTarget"] = "Edit" + endpoint.Kind.ToString();
            return View(endpoint.ToEndpointViewModel());
        }

        // POST: Endpoints/EditHttp/5
        [HttpPost, ActionName("EditHttp")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, HttpEndpointViewModel vm)
        {
            if (id != vm.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var endpoint = vm.ToEndpoint();
                    var oldEndpoint = _context.Endpoints.AsNoTracking().First(e => e.Id == endpoint.Id);
                    _context.Update(endpoint);
                    await _context.SaveChangesAsync();
                    NotifyEditEvent(oldEndpoint, endpoint);
                    _stateService.RemoveStatus(endpoint.Id);
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!EndpointExists(vm.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(List));
            }
            return View(nameof(Edit), vm);
        }

        // POST: Endpoints/EditIcmp/5
        [HttpPost, ActionName("EditIcmp")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, IcmpEndpointViewModel vm)
        {
            if (id != vm.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var endpoint = vm.ToEndpoint();
                    var oldEndpoint = _context.Endpoints.AsNoTracking().First(e => e.Id == endpoint.Id);
                    _context.Update(endpoint);
                    await _context.SaveChangesAsync();
                    NotifyEditEvent(oldEndpoint, endpoint);
                    _stateService.RemoveStatus(endpoint.Id);
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!EndpointExists(vm.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(List));
            }
            return View(nameof(Edit), vm);
        }

        // GET: Endpoints/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var endpoint = await _context.Endpoints.AsQueryable()
                .SingleOrDefaultAsync(m => m.Id == id);
            if (endpoint == null)
            {
                return NotFound();
            }

            return View(endpoint);
        }

        // POST: Endpoints/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var endpoint = await _context.Endpoints.AsQueryable().SingleOrDefaultAsync(m => m.Id == id);
            if (endpoint != null)
            {
                _context.Endpoints.Remove(endpoint);
                await _context.SaveChangesAsync();
                _stateService.RemoveStatus(id);
                NotifyEvent(endpoint, NotificationType.EndpointRemoved);
            }
            return RedirectToAction(nameof(List));
        }

        private bool EndpointExists(int id)
        {
            return _context.Endpoints.Any(e => e.Id == id);
        }

        private void NotifyEvent(Endpoint endpoint, NotificationType type)
        {
            _notifier.NotifyMisc(endpoint, type);
        }

        private void NotifyEditEvent(Endpoint oldEndpoint, Endpoint newEndpoint)
        {
            var enabled = (!oldEndpoint.Enabled && newEndpoint.Enabled);
            var disabled = (oldEndpoint.Enabled && !newEndpoint.Enabled);

            var type = NotificationType.EndpointReconfigured;
            if (enabled) type = NotificationType.EndpointEnabled;
            if (disabled) type = NotificationType.EndpointDisabled;

            _notifier.NotifyMisc(newEndpoint, type);
        }
    }
}
