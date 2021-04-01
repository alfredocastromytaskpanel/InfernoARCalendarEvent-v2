using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Identity.Web;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ToDoListClient.Models;
using ToDoListClient.Services;
using ToDoListClient.Utils;

namespace ToDoListClient.Controllers
{
    [AuthorizeForScopes(ScopeKeySection = "TodoList:TodoListScope")]
    public class ToDoListController : Controller
    {
        private IToDoListService _todoListService;

        public ToDoListController(IToDoListService todoListService)
        {
            _todoListService = todoListService;
        }

        // GET: TodoList
        public async Task<ActionResult> Index()
        {
            TempData["SignedInUser"] = User.GetDisplayName();
            return View(await _todoListService.GetAsync());
        }

        public async Task<IActionResult> CreateEvent(string eventId)
        {
            try
            {
                if (User.Identity.IsAuthenticated)
                {
                    eventId = eventId ?? "248d8ea0-b518-493d-b9c1-0a9f3e4e94c7"; //Just for testing
                    Microsoft.Graph.Event newEvent = await _todoListService.CreateEventAsync(/*(ClaimsIdentity)User.Identity,*/ eventId);
                    ViewData["Response"] = JsonConvert.SerializeObject(newEvent, Formatting.Indented);

                    TempData["Message"] = newEvent != null ? "Success! Your calendar event was created." : "";
                    return RedirectToAction("Index", "Home");
                }
            }
            catch (System.Exception e)
            {
                return RedirectToAction("Error", "Home", new { message = e.Message });
            }
            return RedirectToAction("Index", "Home");
        }

        // GET: TodoList/Create
        public async Task<IActionResult> Create()
        {
            ToDoItem todo = new ToDoItem();
            try
            {
                List<string> result = (await _todoListService.GetAllUsersAsync()).ToList();

                TempData["UsersDropDown"] = result
                .Select(u => new SelectListItem
                {
                    Text = u
                }).ToList();
                TempData["TenantId"] = HttpContext.User.GetTenantId();
                TempData["AssignedBy"] = HttpContext.User.GetDisplayName();
                return View(todo);
            }
            catch (WebApiMsalUiRequiredException ex)
            {
                return Redirect(ex.Message);
            }
        }

        // POST: TodoList/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create([Bind("Title,AssignedTo,AssignedBy,TenantId")] ToDoItem todo)
        {
            await _todoListService.AddAsync(todo);
            return RedirectToAction("Index");
        }

        // GET: TodoList/Edit/5
        public async Task<ActionResult> Edit(int id)
        {
            ToDoItem todo = await this._todoListService.GetAsync(id);

            if (todo == null)
            {
                return NotFound();
            }

            return View(todo);
        }

        // POST: TodoList/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(int id, [Bind("Id,Title,AssignedTo,AssignedBy,TenantId")] ToDoItem todo)
        {
            await _todoListService.EditAsync(todo);
            return RedirectToAction("Index");
        }

        // GET: TodoList/Delete/5
        public async Task<ActionResult> Delete(int id)
        {
            ToDoItem todo = await this._todoListService.GetAsync(id);

            if (todo == null)
            {
                return NotFound();
            }

            return View(todo);
        }

        // POST: TodoList/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Delete(int id, [Bind("Id,Title,AssignedTo")] ToDoItem todo)
        {
            await _todoListService.DeleteAsync(id);
            return RedirectToAction("Index");
        }
    }
}