using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.Utilities;
using EmbedIO.WebApi;
using MHSApi.API;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SecuritySystem.Utils;
using Swan.Formatters;
using Swan.Parsers;

namespace SecuritySystem
{
    public sealed partial class SecurityApiController : WebApiController
    {
        [Route(HttpVerbs.Any, Endpoints.Login)]
        public async Task LoginJson()
        {
            try
            {
                string json = await HttpContext.GetRequestBodyAsStringAsync();
                dynamic c = JObject.Parse(json);
                var username = (string)c.username;
                var password = (string)c.password;
                User? user = null;
                bool NoUsernameOrPassword = false;

                // During inital system setup, allow ANY username or password
                if (!Configuration.Instance.SystemSetUp)
                {
                    var response = new JObject
                    {
                         { "token", new JValue(CreateAuthToken(new User(){ID = 100, Username = "System Installer", Permissions = UserPermissions.Admin})) }
                    };
                    await SendSuccessfulResponseWithContent(response);
                    return;
                }
                if (username != null && password != null)
                {
                    foreach (var item in Configuration.Instance.Users)
                    {
                        if (item.Username == username)
                        {
                            if (item.PasswordHash.Equals(Sha256(password), StringComparison.CurrentCultureIgnoreCase))
                            {
                                user = item;
                            }
                        }
                    }
                }
                else
                {
                    NoUsernameOrPassword = true;
                }

                if (user == null)
                {
                    if (NoUsernameOrPassword)
                    {
                        await SendGenericResponse(SecurityApiResult.NoUsernameOrPassword);
                    }
                    else
                    {
                        SystemManager.WriteToEventLog("Incorrect username/password was entered for user " + username);
                        await SendGenericResponse(SecurityApiResult.IncorrectUsernameOrPassword);
                        return;
                    }
                }
                else
                {
                    var response = new JObject
                    {
                         { "token", new JValue(CreateAuthToken(user)) }
                    };
                    await SendSuccessfulResponseWithContent(response);
                }
            }
            catch
            {
                await SendGenericResponse(SecurityApiResult.InternalError);
            }
        }

        [Route(HttpVerbs.Any, Endpoints.Logout)]
        public async Task LogoutJson()
        {
            string? key;
            if (HttpContext.Request.Headers.ContainsKey("authorization"))
            {
                // fix for WASM builds
                key = HttpContext.Request.Headers["authorization"];
            }
            else if (HttpContext.Request.Headers.ContainsKey("Authorization"))
            {
                key = HttpContext.Request.Headers["Authorization"];
            }
            else
            {
                HttpContext.Response.StatusCode = 401;
                await SendGenericResponse(SecurityApiResult.MissingInvaildAuthToken);
                return;
            }

            // delete authentication token
            if (!string.IsNullOrEmpty(key))
            {
                Configuration.Instance.Tokens.Remove(key);
                Configuration.Save();
            }
            await SendGenericResponse(SecurityApiResult.Success);
        }
        /// <summary>
        /// If user is null, redirects
        /// </summary>
        /// <returns></returns>
        private async Task<User?> GetUserFromToken()
        {
            string? key;
            if (HttpContext.Request.Headers.ContainsKey("authorization"))
            {
                // fix for WASM builds
                key = HttpContext.Request.Headers["authorization"];
            }
            else if (HttpContext.Request.Headers.ContainsKey("Authorization"))
            {
                key = HttpContext.Request.Headers["Authorization"];
            }
            else
            {
                HttpContext.Response.StatusCode = 401;
                await SendGenericResponse(SecurityApiResult.MissingInvaildAuthToken);
                return null;
            }

            if (key != null)
            {
                //verify token from auth header
                foreach (var item in Configuration.Instance.Tokens)
                {
                    if (item.Key == key)
                    {
                        // we found target username, now find user object

                        foreach (var user in Configuration.Instance.Users)
                        {
                            if (user.Username == item.Value)
                            {
                                return user;
                            }
                        }
                        break;
                    }
                }
            }
            // No vaild tokens
            HttpContext.Response.StatusCode = 401;
            await SendGenericResponse(SecurityApiResult.MissingInvaildAuthToken);
            return null;
        }

        [Route(HttpVerbs.Any, Endpoints.CurrentUser)]
        public async Task CurrentUserApi()
        {
            User? currentUser = await GetUserFromToken();
            if (currentUser == null) return;

            if (HttpContext.Request.HttpVerb == HttpVerbs.Get)
            {
                await SendSuccessfulResponseWithContent(Json2ApiUser(currentUser));
            }
            else if (HttpContext.Request.HttpVerb == HttpVerbs.Patch)
            {
                string json = await HttpContext.GetRequestBodyAsStringAsync();
                UserUpdateRequest? c = JsonConvert.DeserializeObject<UserUpdateRequest>(json);
                if (c == null)
                {
                    await SendGenericResponse(SecurityApiResult.BadRequest);
                    return;
                }

                if (c.type == UserUpdateRequestType.None)
                {
                    await SendGenericResponse(SecurityApiResult.Success);
                }
                else if (c.type == UserUpdateRequestType.Password)
                {
                    UserUpdatePasswordRequest? fullRequest = JsonConvert.DeserializeObject<UserUpdatePasswordRequest>(json);

                    if (fullRequest == null)
                    {
                        await SendGenericResponse(SecurityApiResult.BadRequest);
                        return;
                    }

                    // check if old password is correct
                    if (currentUser.PasswordHash.Equals(Sha256(fullRequest.OldPassword), StringComparison.CurrentCultureIgnoreCase))
                    {
                        // old passowrd is fine, validate new password and change it
                        if (!string.IsNullOrEmpty(fullRequest.NewPassword))
                        {
                            currentUser.PasswordHash = Sha256(fullRequest.NewPassword);
                            Configuration.Save();
                            await SendGenericResponse(SecurityApiResult.Success);
                        }
                        else
                        {
                            await SendGenericResponse(SecurityApiResult.BadRequest);
                        }
                    }
                    else
                    {
                        // it is wrong
                        await SendGenericResponse(SecurityApiResult.IncorrectUsernameOrPassword);
                    }
                    await SendGenericResponse(SecurityApiResult.Success);
                }
                else if (c.type == UserUpdateRequestType.Permission)
                {
                    // current user api shouldn't be able to change permissions for the current user
                    await SendGenericResponse(SecurityApiResult.BadRequest);
                }
                else
                {
                    await SendGenericResponse(SecurityApiResult.NotImplemented);
                }
            }
            else
            {
                // we don't support other request yet as mhsclient needs user page
                await SendGenericResponse(SecurityApiResult.NotImplemented);
            }
        }

        private static ApiUser Json2ApiUser(User currentUser)
        {
            return new ApiUser() { ID = currentUser.ID, Username = currentUser.Username, Permissions = currentUser.Permissions };
        }

        [Route(HttpVerbs.Get, Endpoints.AllUser)]
        public async Task AllUsersApi()
        {
            User? currentUser = await GetUserFromToken();
            if (currentUser == null) return;
            if (currentUser.Permissions != UserPermissions.Admin)
            {
                await SendGenericResponse(SecurityApiResult.NoPermission);
                return;
            }

            List<ApiUser> response = [];
            foreach (var item in Configuration.Instance.Users)
            {
                response.Add(Json2ApiUser(item));
            }

            await SendSuccessfulResponseWithContent(response.ToArray());
        }

        [Route(HttpVerbs.Any, Endpoints.UserMod + "{id}")]
        public async Task ModUserApi(string id)
        {
            User? currentUser = await GetUserFromToken();
            if (currentUser == null) return;
            if (currentUser.Permissions != UserPermissions.Admin)
            {
                await SendGenericResponse(SecurityApiResult.NoPermission);
                return;
            }

            if (HttpContext.Request.HttpVerb == HttpVerbs.Patch)
            {
                string json = await HttpContext.GetRequestBodyAsStringAsync() ?? throw new HttpException(System.Net.HttpStatusCode.BadRequest);
                UserUpdateRequest? type = JsonConvert.DeserializeObject<UserUpdateRequest>(json) ?? throw new HttpException(System.Net.HttpStatusCode.BadRequest);
                if (type.type == UserUpdateRequestType.Permission)
                {
                    UserUpdatePermissionRequest? perm = JsonConvert.DeserializeObject<UserUpdatePermissionRequest>(json) ?? throw new HttpException(System.Net.HttpStatusCode.BadRequest);

                    // verify that at least 1 user has admin permission AFTER changing target user permission
                    // when changing admin permission to user
                    if (perm.Permissions == UserPermissions.User)
                    {
                        bool hasAdmin = false;
                        foreach (var item in Configuration.Instance.Users)
                        {
                            if (item.Permissions == UserPermissions.Admin && item.ID.ToString() != id)
                            {
                                hasAdmin = true;
                            }
                        }

                        if (!hasAdmin)
                        {
                            await SendUnSuccessfulResponseWithCustomMessage("User check failed. At least 1 user must have administrator access.");
                            return;
                        }
                    }

                    foreach (var item in Configuration.Instance.Users)
                    {
                        if (item.ID.ToString() == id)
                        {
                            item.Permissions = perm.Permissions;
                            Configuration.Save();
                            await SendGenericResponse(SecurityApiResult.Success);
                            return;
                        }
                    }

                    await SendGenericResponse(SecurityApiResult.FileNotFound);

                }
                else if (type.type == UserUpdateRequestType.Password)
                {
                    UserUpdatePasswordRequest? pw = JsonConvert.DeserializeObject<UserUpdatePasswordRequest>(json) ?? throw new HttpException(System.Net.HttpStatusCode.BadRequest);
                    foreach (var item in Configuration.Instance.Users)
                    {
                        if (item.ID.ToString() == id)
                        {
                            item.PasswordHash = Sha256(pw.NewPassword);
                            Configuration.Save();
                            await SendGenericResponse(SecurityApiResult.Success);
                            return;
                        }
                    }

                    await SendGenericResponse(SecurityApiResult.FileNotFound);
                }
                else
                {
                    await SendGenericResponse(SecurityApiResult.BadRequest);
                }
            }
            else if (HttpContext.Request.HttpVerb == HttpVerbs.Delete)
            {
                if (Configuration.Instance.Users.Count == 1)
                {
                    // cannot delete current (and only user)
                    await SendUnSuccessfulResponseWithCustomMessage("User check failed. There must be at least 1 user account in the system.");
                    return;
                }

                foreach (var item in Configuration.Instance.Users)
                {
                    if (item.ID.ToString() == id)
                    {
                        Configuration.Instance.Users.Remove(item);
                        Configuration.Save();
                        await SendGenericResponse(SecurityApiResult.Success);
                        return;
                    }
                }

                await SendGenericResponse(SecurityApiResult.FileNotFound);
            }
            else if (HttpContext.Request.HttpVerb == HttpVerbs.Post)
            {
                if (id == "new")
                {
                    // create new user

                    ApiUser c = await ParseRequestJson<ApiUser>();

                    // do some basic checks
                    if (string.IsNullOrEmpty(c.Username) || string.IsNullOrEmpty(c.Password))
                    {
                        await SendGenericResponse(SecurityApiResult.NoUsernameOrPassword);
                        return;
                    }

                    // create the user
                    Configuration.Instance.Users.Add(new User() { Username = c.Username, PasswordHash = Sha256(c.Password), Permissions = c.Permissions, ID = -1 });

                    // save config
                    Configuration.Save();

                    await SendGenericResponse(SecurityApiResult.Success);
                }
                else
                {
                    await SendGenericResponse(SecurityApiResult.BadRequest);
                }
            }
            else
            {
                await SendGenericResponse(SecurityApiResult.BadRequest);
            }
        }
    }
}
