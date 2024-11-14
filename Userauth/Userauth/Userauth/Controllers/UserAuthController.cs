using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using System;
using System.Linq;
using System.Threading.Tasks;
using Userauth.Models;

namespace Userauth.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserAuthController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<UserAuthController> _logger;

        public UserAuthController(ILogger<UserAuthController> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        // API for Non-Azure AD Login
        [HttpPost("NonAzureAdLogin")]
        public ActionResult NonAzureAdLogin([FromBody] NonAzureAdLoginInput credential)
        {
            _logger.LogTrace($"Non-Azure AD Login request received for Username: {credential.UserName}");

            // Check for hardcoded user credentials
            if (credential.UserName.Equals("sagnik@gmail.com", StringComparison.OrdinalIgnoreCase) && credential.Password == "1234")
            {
                _logger.LogTrace("Hardcoded user login successful");
                return Ok("Login successful");
            }
            else
            {
                _logger.LogWarning("Invalid credentials for non-Azure AD user");
                return Unauthorized("Invalid credentials. Please check your username and password.");
            }
        }

        // API for Azure AD Login
        [HttpPost("AzureAdLogin")]
        public async Task<ActionResult> AzureAdLogin([FromBody] AzureAdLoginInput credential)
        {
            _logger.LogTrace($"Azure AD Login request received for Email: {credential.Email}");

            var app = PublicClientApplicationBuilder.Create(_configuration["AzureAd:ClientId"])
                .WithAuthority(new Uri($"https://login.microsoftonline.com/{_configuration["AzureAd:TenantId"]}"))
                .WithRedirectUri("http://localhost:63063/silent.html") // Make sure this matches your Azure AD Redirect URI
                .Build();

            try
            {
                var accounts = await app.GetAccountsAsync();
                var account = accounts.FirstOrDefault(a => a.Username.Equals(credential.Email, StringComparison.OrdinalIgnoreCase));

                if (account != null)
                {
                    var result = await app.AcquireTokenSilent(new[] { "User.Read" }, account)
                                          .ExecuteAsync();

                    _logger.LogTrace("Email has been authenticated successfully");
                    return Ok("Login successful");
                }
                else
                {
                    _logger.LogWarning($"No cached account found for email: {credential.Email}. Attempting interactive login...");
                    var result = await app.AcquireTokenInteractive(new[] { "User.Read" })
                                          .WithLoginHint(credential.Email)
                                          .WithPrompt(Prompt.NoPrompt) // Avoid the "pick an account" prompt
                                          .ExecuteAsync();

                    if (result.Account.Username.Equals(credential.Email, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogTrace("Email has been authenticated successfully via interactive login");
                        return Ok("Login successful");
                    }
                    else
                    {
                        _logger.LogWarning($"Email mismatch: {credential.Email} does not match authenticated email: {result.Account.Username}");
                        return Unauthorized("User is not authorized");
                    }
                }
            }
            catch (MsalUiRequiredException)
            {
                _logger.LogWarning("Interactive login required but was not successful.");
                return Unauthorized("Interactive login required.");
            }
            catch (MsalServiceException ex)
            {
                if (ex.ErrorCode == "invalid_grant" || ex.ErrorCode == "access_denied")
                {
                    _logger.LogWarning($"Invalid credentials or access denied for user {credential.Email}: {ex.Message}");
                    return Unauthorized("Access denied. Please check your email.");
                }
                _logger.LogError($"MSAL service error during authentication for user {credential.Email}: {ex.Message}");
                return Unauthorized("Authentication failed");
            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred during MSAL authentication: {ex.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
            }
        }
    }
}
