using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using sama;
using sama.Models;

namespace sama.Controllers
{
    public class EndpointsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public EndpointsController(ApplicationDbContext context)
        {
            _context = context;    
        }

        // GET: Endpoints
        public async Task<IActionResult> Index()
        {
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
                return RedirectToAction("Index");
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
                return RedirectToAction("Index");
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
            return RedirectToAction("Index");
        }

        private bool EndpointExists(int id)
        {
            return _context.Endpoints.Any(e => e.Id == id);
        }
    }
}
