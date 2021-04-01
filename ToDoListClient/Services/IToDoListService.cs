using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ToDoListClient.Models;

namespace ToDoListClient.Services
{
    public interface IToDoListService
    {
        Task<IEnumerable<ToDoItem>> GetAsync();

        Task<ToDoItem> GetAsync(int id);
        Task<IEnumerable<string>> GetAllUsersAsync();
        Task DeleteAsync(int id);

        Task<ToDoItem> AddAsync(ToDoItem todo);

        Task<ToDoItem> EditAsync(ToDoItem todo);

        Task<Microsoft.Graph.Event> CreateEventAsync(/*ClaimsIdentity userIdentity,*/ string eventId);
    }
}
