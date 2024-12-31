using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Functions.Worker.Http;
using JTI.CodeGen.API.Models.Constants;
using System.Net;
using Newtonsoft.Json;
using JTI.CodeGen.API.Models.Entities;
using JTI.CodeGen.API.UserModule.Helpers;
using JTI.CodeGen.API.UserModule.Dtos;
using AutoMapper;
using JTI.CodeGen.API.Models.Enums;
using JTI.CodeGen.API.Common.Services.Interfaces;
using JTI.CodeGen.API.Common.Helpers;
using JTI.CodeGen.API.UserModule.Services.Interfaces;

namespace JTI.CodeGen.API.UserModule
{
    public class UserFunction
    {
        private readonly ILogger<UserFunction> _logger;
        private readonly IUserService _userService;
        private readonly IMapper _mapper;
        private readonly IJwtService _jwtService;

        public UserFunction(ILogger<UserFunction> logger, IUserService userService, IMapper mapper, IJwtService jwtService)
        {
            _logger = logger;
            _userService = userService;
            _mapper = mapper;
            _jwtService = jwtService;
        }

        [Function("get-all-users")]
        public async Task<HttpResponseData> GetAllUsers([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req, FunctionContext context)
        {
            _logger.LogInformation("[Get All Users] Function Started.");

            // Define a list of valid roles
            var validRoles = new List<string> { ApprRoleEnum.SuperAdmin.ToString() };
            // Check if the user is authenticated and has a valid role
            if (await AuthHelper.CheckAuthenticationAndAuthorization(context, req, validRoles) is HttpResponseData authResponse)
            {
                return authResponse;
            }

            var pageSize = int.TryParse(req.Query[HttpParameterConstants.PageSize], out var ps) ? ps : ConfigurationConstants.defaultPageSize;

            var allUsers = await _userService.GetAllUsersAsync();

            if (allUsers == null || !allUsers.Any())
            {
                _logger.LogWarning("[Get All Users] No users found.");
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteAsJsonAsync(new { message = "No users found" });
                return notFoundResponse;
            }

            var mapped = _mapper.Map<List<UserDto>>(allUsers);

            var response = allUsers.Any()
                ? req.CreateResponse(HttpStatusCode.OK)
                : req.CreateResponse(HttpStatusCode.NotFound);

            await response.WriteAsJsonAsync(mapped);

            _logger.LogInformation("[Get All Users] Function Completed.");

            return response;
        }

        [Function("get-users-paginated")]
        public async Task<HttpResponseData> GetUsersPaginated([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req, FunctionContext context)
        {
            _logger.LogInformation("[Get Users Paginated] Function Started.");

            // Define a list of valid roles
            var validRoles = new List<string> { ApprRoleEnum.SuperAdmin.ToString() };
            // Check if the user is authenticated and has a valid role
            if (await AuthHelper.CheckAuthenticationAndAuthorization(context, req, validRoles) is HttpResponseData authResponse)
            {
                return authResponse;
            }

            var pageSize = int.TryParse(req.Query[HttpParameterConstants.PageSize], out var ps) ? ps : ConfigurationConstants.defaultPageSize;

            var allUsers = await _userService.GetAllUsersAsync();

            if (allUsers == null || !allUsers.Any())
            {
                _logger.LogWarning("[Get Users Paginated] No users found.");
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteAsJsonAsync(new { message = "No users found" });
                return notFoundResponse;
            }

            var mappedUsers = _mapper.Map<List<UserDto>>(allUsers);

            // Organize users into a dictionary for pagination
            var userDictionary = new Dictionary<int, List<UserDto>>();
            int totalPages = (int)Math.Ceiling((double)mappedUsers.Count() / pageSize);

            for (int i = 1; i <= totalPages; i++)
            {
                var pageUsers = mappedUsers.Skip((i - 1) * pageSize).Take(pageSize).ToList();
                userDictionary.Add(i, pageUsers);
            }

            var response = allUsers.Any()
                ? req.CreateResponse(HttpStatusCode.OK)
                : req.CreateResponse(HttpStatusCode.NotFound);

            await response.WriteAsJsonAsync(userDictionary);

            _logger.LogInformation("[Get Users Paginated] Function Completed.");

            return response;
        }

        [Function("get-user-by-email")]
        public async Task<HttpResponseData> GetUserByEmail([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req, FunctionContext context)
        {
            _logger.LogInformation("[Get User By Email] Function Started.");

            // Define a list of valid roles
            var validRoles = new List<string> { ApprRoleEnum.SuperAdmin.ToString() };
            // Check if the user is authenticated and has a valid role
            if (await AuthHelper.CheckAuthenticationAndAuthorization(context, req, validRoles) is HttpResponseData authResponse)
            {
                return authResponse;
            }

            // Ensure the query parameter name matches
            var email = req.Query[HttpParameterConstants.Email];

            if (string.IsNullOrWhiteSpace(email))
            {
                _logger.LogInformation("[Get User By Email] Email Parameter Null or Empty.");
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Email parameter is required.");
                return badRequestResponse;
            }

            var result = await _userService.GetByEmailAsync(email);

            var response = result is not null ? req.CreateResponse(HttpStatusCode.OK)
                : req.CreateResponse(HttpStatusCode.NotFound);

            if (result is not null)
            {
                await response.WriteAsJsonAsync(result);
            }
            else
            {
                await response.WriteStringAsync("User not found.");
            }

            return response;
        }

        [Function("create-user")]
        public async Task<HttpResponseData> CreateUser([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req, FunctionContext context)
        {
            _logger.LogInformation("[Create User] Function Started.");

            // Define a list of valid roles
            var validRoles = new List<string> { ApprRoleEnum.SuperAdmin.ToString() };
            // Check if the user is authenticated and has a valid role
            if (await AuthHelper.CheckAuthenticationAndAuthorization(context, req, validRoles) is HttpResponseData authResponse)
            {
                return authResponse;
            }

            // Read and parse the request body
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var createUserRequest = JsonConvert.DeserializeObject<CreateUserRequest>(requestBody);

            // Check if all required parameters are present
            if (createUserRequest == null || string.IsNullOrEmpty(createUserRequest.Email) || string.IsNullOrEmpty(createUserRequest.UserName) || string.IsNullOrEmpty(createUserRequest.Password) || string.IsNullOrEmpty(createUserRequest.Brand) || string.IsNullOrEmpty(createUserRequest.AppRole))
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Missing required user parameters.");
                return badRequestResponse;
            }

            // Validate Brand and AppRole
            if (!Enum.TryParse(createUserRequest.Brand, out BrandEnum brand) || !Enum.TryParse(createUserRequest.AppRole, out ApprRoleEnum appRole))
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Invalid Brand or AppRole.");
                return badRequestResponse;
            }

            // Check if the user already exists by email or username
            var existingUserByEmail = await _userService.GetByEmailAsync(createUserRequest.Email);
            var existingUserByUsername = await _userService.GetByUsernameAsync(createUserRequest.UserName);

            if (existingUserByEmail != null || existingUserByUsername != null)
            {
                var conflictResponse = req.CreateResponse(HttpStatusCode.Conflict);
                await conflictResponse.WriteStringAsync("User with the same email or username already exists.");
                return conflictResponse;
            }

            // Hash the password before storing it
            createUserRequest.Password = PasswordHelper.HashPassword(createUserRequest.Password);

            // Map the DTO to the User entity
            var user = _mapper.Map<User>(createUserRequest);

            // Assign a new ID to the user
            user.id = Guid.NewGuid().ToString();

            // Insert the new user
            var result = await _userService.AddUserAsync(user);

            var response = result is not null ? req.CreateResponse(HttpStatusCode.OK)
                : req.CreateResponse(HttpStatusCode.BadRequest);

            await response.WriteAsJsonAsync(result);

            return response;
        }

        [Function("login")]
        public async Task<HttpResponseData> Login([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req, FunctionContext executionContext)
        {
            _logger.LogInformation("[Login] Function Started.");

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var loginRequest = JsonConvert.DeserializeObject<LoginRequest>(requestBody);

            // Retrieve user from the database by username or email
            var user = await _userService.GetByUsernameAsync(loginRequest.UsernameOrEmail)
                       ?? await _userService.GetByEmailAsync(loginRequest.UsernameOrEmail);

            if (user != null && PasswordHelper.VerifyPassword(loginRequest.Password, user.HashedPassword))
            {
                var token = _jwtService.GenerateJwtToken(user.UserName);
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { token });
                return response;
            }

            var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
            await unauthorizedResponse.WriteStringAsync("Invalid username or password.");
            return unauthorizedResponse;
        }
    }
}
