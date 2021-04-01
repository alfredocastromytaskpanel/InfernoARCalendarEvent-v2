using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ToDoListService.Models;
using Microsoft.Identity.Web.Resource;
using Microsoft.Identity.Web;
using Microsoft.Identity.Client;
using System.Net.Http.Headers;
using Microsoft.Graph;
using System.Net;
using System.Security.Claims;
using System.Net.Http;
using Newtonsoft.Json;
using ToDoListService.Extensions;
using Microsoft.Extensions.Configuration;

namespace ToDoListService.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [RequiredScope("access_as_user")]
    [ApiController]
    public class TodoListController : ControllerBase
    {
        private readonly TodoContext _context;
        private ITokenAcquisition _tokenAcquisition;
        private readonly IConfiguration _configuration;

        public TodoListController(TodoContext context, ITokenAcquisition tokenAcquisition, IConfiguration configuration)
        {
            _context = context;
            _tokenAcquisition = tokenAcquisition;
            _configuration = configuration;
        }

        [HttpGet("createevent/{id}")]
        public async Task<ActionResult<Event>> CreateEvent(string id = null)
        {
            //id = id ?? "248d8ea0-b518-493d-b9c1-0a9f3e4e94c7"; //Just for testing
            try
            {
                Event newEvent = await CallGraphApiOnBehalfOfUserForEvent(id);
                return newEvent;
            }
            catch (MsalUiRequiredException ex)
            {
                HttpContext.Response.ContentType = "text/plain";
                HttpContext.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                await HttpContext.Response.WriteAsync("An authentication error occurred while acquiring a token for downstream API\n" + ex.ErrorCode + "\n" + ex.Message);
            }

            return null;
        }

        // GET: api/TodoItems
        [HttpGet]
        public async Task<ActionResult<IEnumerable<TodoItem>>> GetTodoItems()
        {
            string userTenantId = HttpContext.User.GetTenantId();
            var signedInUser = HttpContext.User.GetDisplayName();
            try
            {
                await _context.TodoItems.ToListAsync();
            }
            catch(Exception)
            {
                throw;
            }
            return await _context.TodoItems.Where
                (x => x.TenantId == userTenantId && (x.AssignedTo == signedInUser || x.Assignedby== signedInUser)).ToListAsync();
        }

        // GET: api/TodoItems/5
        [HttpGet("{id}")]
        public async Task<ActionResult<TodoItem>> GetTodoItem(int id)
        { 
            var todoItem = await _context.TodoItems.FindAsync(id);

            if (todoItem == null)
            {
                return NotFound();
            }

            return todoItem;
        }

        [HttpGet("getallusers")]
        public async Task<ActionResult<IEnumerable<string>>> GetAllUsers()
        {
            try
            {
                List<string> Users = await CallGraphApiOnBehalfOfUser();
                return Users;
            }
            catch (MsalUiRequiredException ex)
            {
                HttpContext.Response.ContentType = "text/plain";
                HttpContext.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                await HttpContext.Response.WriteAsync("An authentication error occurred while acquiring a token for downstream API\n" + ex.ErrorCode + "\n" + ex.Message);
            }

            return null;
        }
        // PUT: api/TodoItems/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for
        // more details see https://aka.ms/RazorPagesCRUD.
        [HttpPut("{id}")]
        public async Task<IActionResult> PutTodoItem(int id, TodoItem todoItem)
        {
            if (id != todoItem.Id)
            {
                return BadRequest();
            }

            _context.Entry(todoItem).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!TodoItemExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return Ok(todoItem);
        }

        // POST: api/TodoItems
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for
        // more details see https://aka.ms/RazorPagesCRUD.
        [HttpPost]
        public async Task<ActionResult<TodoItem>> PostTodoItem(TodoItem todoItem)
        {
            var random = new Random();
            todoItem.Id = random.Next();

            
            _context.TodoItems.Add(todoItem);
            await _context.SaveChangesAsync();

            //return CreatedAtAction("GetTodoItem", new { id = todoItem.Id }, todoItem);
            return Ok(todoItem);
        }

        // DELETE: api/TodoItems/5
        [HttpDelete("{id}")]
        public async Task<ActionResult<TodoItem>> DeleteTodoItem(int id)
        {
            var todoItem = await _context.TodoItems.FindAsync(id);
            if (todoItem == null)
            {
                return NotFound();
            }

            _context.TodoItems.Remove(todoItem);
            await _context.SaveChangesAsync();

            return todoItem;
        }

        private bool TodoItemExists(int id)
        {
            return _context.TodoItems.Any(e => e.Id == id);
        }
        public async Task<Event> CallGraphApiOnBehalfOfUserForEvent(string eventId)
        {
            string[] scopes = { "user.read", "user.readbasic.all", "mail.send", "calendars.readwrite", "calendars.readwrite.shared", "user.read.all" };

            // we use MSAL.NET to get a token to call the API On Behalf Of the current user
            try
            {
                string infernoAPIKey = _configuration.GetValue<string>("InfernoAPIKey");
                //ClaimsIdentity claimsIdentity = HttpContext.User.Identity as ClaimsIdentity;
                string accessToken = await _tokenAcquisition.GetAccessTokenForUserAsync(scopes);
                Event newEvent = await CallGraphApiOnBehalfOfUserForEvent(infernoAPIKey, eventId, accessToken /*, claimsIdentity*/);
                return newEvent;
            }
            catch (MsalUiRequiredException ex)
            {
                await _tokenAcquisition.ReplyForbiddenWithWwwAuthenticateHeaderAsync(scopes, ex);
                throw ex;
            }
        }
        private static async Task<Event> CallGraphApiOnBehalfOfUserForEvent(string infernoAPIKey, string eventId, string accessToken /*, ClaimsIdentity claimsIdentity = null*/)
        {
            // Call the Graph API and retrieve the user's profile.
            GraphServiceClient graphServiceClient = GetGraphServiceClient(accessToken /*, claimsIdentity*/);

            var me = await graphServiceClient.Me.Request().GetAsync();

            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + infernoAPIKey);

                Event newEvent = null;
                string tzName = "Pacific Standard Time";
                try
                {
                    var response = await httpClient.GetAsync("https://api.infernocore.jolokia.com/api/Events/" + eventId);
                    if (!response.IsSuccessStatusCode) return null;
                    string apiResponse = await response.Content.ReadAsStringAsync();
                    var infEvent = JsonConvert.DeserializeObject<InfernoEvent>(apiResponse);
                    tzName = infEvent.startTime.GetTimeZoneStandardName();
                    newEvent = infEvent.ToMSGraphEvent();
                }
                catch (Exception)
                {
                    //Default event just to keep debug without api.infernocore
                    newEvent = new Event
                    {
                        Subject = "Let's go for lunch",
                        Body = new ItemBody
                        {
                            ContentType = BodyType.Html,
                            Content = "Does noon work for you?"
                        },
                        Start = new DateTimeTimeZone
                        {
                            DateTime = "2021-03-30T10:00:00",
                            TimeZone = "Pacific Standard Time"
                        },
                        End = new DateTimeTimeZone
                        {
                            DateTime = "2021-03-30T11:00:00",
                            TimeZone = "Pacific Standard Time"
                        },
                        Attendees = new List<Attendee>()
                        {
                            new Attendee
                            {
                                EmailAddress = new EmailAddress
                                {
                                    Address = me.UserPrincipalName,
                                    Name = me.DisplayName
                                },
                                Type = AttendeeType.Required
                            }
                        }
                    };
                }
                var createdEvent = await graphServiceClient.Me.Events
                                        .Request()
                                        .Header("Prefer", $"outlook.timezone=\"{tzName}\"") //"outlook.timezone=\"Pacific Standard Time\"" $"outlook.timezone=\"{tzName}\""
                                        .AddAsync(newEvent);
                if (createdEvent != null)
                {
                    return createdEvent;
                }
            }
            throw new Exception();
        }
        public async Task<List<string>> CallGraphApiOnBehalfOfUser()
        {
            string[] scopes = { "user.read.all" };

            // we use MSAL.NET to get a token to call the API On Behalf Of the current user
            try
            {
                List<string> userList = new List<string>();
                string accessToken = await _tokenAcquisition.GetAccessTokenForUserAsync(scopes);
                IEnumerable<User> users = await CallGraphApiOnBehalfOfUser(accessToken);
                userList = users.Select(x => x.UserPrincipalName).ToList();
                return userList;
            }
            catch (MsalUiRequiredException ex)
            {
                await _tokenAcquisition.ReplyForbiddenWithWwwAuthenticateHeaderAsync(scopes, ex);
                throw ex;
            }
        }
        private static async Task<IEnumerable<User>> CallGraphApiOnBehalfOfUser(string accessToken)
        {
            // Call the Graph API and retrieve the user's profile.
            GraphServiceClient graphServiceClient = GetGraphServiceClient(accessToken);
            IGraphServiceUsersCollectionPage users = await graphServiceClient.Users.Request()
                                                      .Filter($"accountEnabled eq true")
                                                      .Select("id, userPrincipalName")
                                                      .GetAsync();
            if (users != null)
            {

                return users;
            }
            throw new Exception();
        }
        /// <summary>
        /// Prepares the authenticated client.
        /// </summary>
        /// <param name="accessToken">The access token.</param>
        private static GraphServiceClient GetGraphServiceClient(string accessToken /*, ClaimsIdentity userIdentity = null*/)
        {
            try
            {
                /***
                //Microsoft Azure AD Graph API endpoint,
                'https://graph.microsoft.com'   Microsoft Graph global service
                'https://graph.microsoft.us' Microsoft Graph for US Government
                'https://graph.microsoft.de' Microsoft Graph Germany
                'https://microsoftgraph.chinacloudapi.cn' Microsoft Graph China
                 ***/

                //string ObjectIdentifierType = "http://schemas.microsoft.com/identity/claims/objectidentifier";
                //string TenantIdType = "http://schemas.microsoft.com/identity/claims/tenantid";

                //var identifier = userIdentity.FindFirst(ObjectIdentifierType)?.Value + "." + userIdentity.FindFirst(TenantIdType)?.Value;

                //string[] _scopes = { "user.read", "user.readbasic.all", "mail.send", "calendars.readwrite", "calendars.readwrite.shared", "user.read.all" };
                //var ClientId = "58d7da62-e3b0-4848-a243-70b2a2fb98e8";
                //var ClientSecret = "W-_E2-oq043YfwC06~SPANPrw00bI~H4Bd";
                //var BaseUrl = "https://localhost:44351";
                //var CallbackPath = "/signin-oidc";
                //IConfidentialClientApplication _app = ConfidentialClientApplicationBuilder.Create(ClientId)
                //    .WithClientSecret(ClientSecret)
                //    .WithAuthority(AzureCloudInstance.AzurePublic, AadAuthorityAudience.AzureAdAndPersonalMicrosoftAccount)
                //    .WithRedirectUri(BaseUrl + CallbackPath)
                //    .Build();
                //var account = _app.GetAccountAsync(identifier).Result;
                //if (account == null) throw new ServiceException(new Error
                //{
                //    Code = "TokenNotFound",
                //    Message = "User not found in token cache. Maybe the server was restarted."
                //});
                //var result = _app.AcquireTokenSilent(_scopes, account).ExecuteAsync().Result;
                //accessToken = result.AccessToken;

                //GraphServiceClient graphServiceClient = new GraphServiceClient(new DelegateAuthenticationProvider(
                //async requestMessage =>
                //{
                //    // Get user's id for token cache.
                //    var identifier = userIdentity.FindFirst(ObjectIdentifierType)?.Value + "." + userIdentity.FindFirst(TenantIdType)?.Value;

                //    // Passing tenant ID to the sample auth provider to use as a cache key
                //    //var accessToken = await _authProvider.GetUserAccessTokenAsync(identifier);

                //    // Append the access token to the request
                //    requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                //    // This header identifies the sample in the Microsoft Graph service. If extracting this code for your project please remove.
                //    requestMessage.Headers.Add("SampleID", "aspnetcore-connect-sample");
                //}));

                string graphEndpoint = "https://graph.microsoft.com/v1.0/";
                GraphServiceClient graphServiceClient = new GraphServiceClient(graphEndpoint,
                                                                     new DelegateAuthenticationProvider(
                                                                         async (requestMessage) =>
                                                                         {
                                                                             await Task.Run(() =>
                                                                             {
                                                                                 requestMessage.Headers.Authorization = new AuthenticationHeaderValue("bearer", accessToken);
                                                                             });
                                                                         }));
                return graphServiceClient;
            }
            catch (Exception ex)
            {
                return null;   
            }
        }
    }
}
