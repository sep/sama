using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using sama.Models;
using sama.Services;

namespace sama.Controllers
{
    public class EndpointsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly StateService _stateService;

        public EndpointsController(ApplicationDbContext context, StateService stateService)
        {
            _context = context;
            _stateService = stateService;
        }

        public IActionResult IndexRedirect()
        {
            return RedirectToAction(nameof(Index));
        }

        // GET: Endpoints/Index
        public async Task<IActionResult> Index()
        {
            ViewData.Add("CurrentStates", _stateService.GetAllStates());
            return View(await _context.Endpoints.ToListAsync());
        }

        // GET: Endpoints/List
        public async Task<IActionResult> List()
        {
            ViewData.Add("CurrentStates", _stateService.GetAllStates());
            return View(await _context.Endpoints.ToListAsync());
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

            return View(endpoint);
        }

        // GET: Endpoints/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Endpoints/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Enabled,Name,Location,ResponseMatch")] Endpoint endpoint)
        {
            if (ModelState.IsValid)
            {
                _context.Add(endpoint);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(List));
            }
            return View(endpoint);
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
            return View(endpoint);
        }

        // POST: Endpoints/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Enabled,Name,Location,ResponseMatch")] Endpoint endpoint)
        {
            if (id != endpoint.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(endpoint);
                    await _context.SaveChangesAsync();
                    _stateService.SetState(endpoint, null, null);
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!EndpointExists(endpoint.Id))
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
            return View(endpoint);
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
