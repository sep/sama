using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using sama.Models;
using sama.Services;
using Microsoft.AspNetCore.Authorization;
using sama.Extensions;

namespace sama.Controllers
{
    [Authorize]
    public class EndpointsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly StateService _stateService;
        private readonly UserManagementService _userService;

        public EndpointsController(ApplicationDbContext context, StateService stateService, UserManagementService userService)
        {
            _context = context;
            _stateService = stateService;
            _userService = userService;
        }

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
            ViewData.Add("CurrentStates", _stateService.GetAllStates());

            var endpoints = await _context.Endpoints.ToListAsync();
            return View(endpoints.Select(e => e.ToEndpointViewModel()));
        }

        // GET: Endpoints/List
        public async Task<IActionResult> List()
        {
            ViewData.Add("CurrentStates", _stateService.GetAllStates());

            var endpoints = await _context.Endpoints.ToListAsync();
            return View(endpoints.Select(e => e.ToEndpointViewModel()));
        }

        // GET: Endpoints/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var endpoint = await _context.Endpoints
                .SingleOrDefaultAsync(m => m.Id == id);
            if (endpoint == null)
            {
                return NotFound();
            }

            ViewData.Add("State", _stateService.GetState(endpoint.Id));

            return View(endpoint.ToEndpointViewModel());
        }

        // GET: Endpoints/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Endpoints/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(HttpEndpointViewModel vm)
        {
            if (ModelState.IsValid)
            {
                var endpoint = vm.ToEndpoint();
                endpoint.Id = 0;
                _context.Add(endpoint);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(List));
            }
            return View(vm);
        }

        // GET: Endpoints/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var endpoint = await _context.Endpoints.SingleOrDefaultAsync(m => m.Id == id);
            if (endpoint == null)
            {
                return NotFound();
            }

            return View(endpoint.ToEndpointViewModel());
        }

        // POST: Endpoints/Edit/5
        [HttpPost, ActionName("Edit")]
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
                    _context.Update(endpoint);
                    await _context.SaveChangesAsync();
                    _stateService.SetState(endpoint, null, null);
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
            return View(vm);
        }

        // GET: Endpoints/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var endpoint = await _context.Endpoints
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
            var endpoint = await _context.Endpoints.SingleOrDefaultAsync(m => m.Id == id);
            _context.Endpoints.Remove(endpoint);
            await _context.SaveChangesAsync();
            _stateService.RemoveState(id);
            return RedirectToAction(nameof(List));
        }

        private bool EndpointExists(int id)
        {
            return _context.Endpoints.Any(e => e.Id == id);
        }
    }
}
