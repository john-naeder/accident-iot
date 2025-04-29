using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AccidentMonitor.WebApp.Models;
using Blazored.LocalStorage;

namespace AccidentMonitor.WebApp.Services;

public interface IAuthService
{
    Task<LoginResponse> Login(LoginRequest loginRequest);
    Task<RegistrationResponse> Register(RegistrationRequest registrationRequest);
    Task Logout();
    Task<UserInfo?> GetUserInfo();
}

public class AuthService : IAuthService
{
    private readonly HttpClient _httpClient;
    private readonly ILocalStorageService _localStorage;
    private readonly JsonSerializerOptions _jsonOptions;

    public AuthService(HttpClient httpClient, ILocalStorageService localStorage)
    {
        _httpClient = httpClient;
        _localStorage = localStorage;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }    public async Task<LoginResponse> Login(LoginRequest loginRequest)
    {
        try
        {
            var requestJson = JsonSerializer.Serialize(loginRequest);
            var requestContent = new StringContent(requestJson, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync("api/Users/login", requestContent);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return new LoginResponse 
                { 
                    Successful = false, 
                    Error = $"Error: {response.StatusCode} - {content}" 
                };
            }

            var result = JsonSerializer.Deserialize<LoginResponse>(content, _jsonOptions);
            if (result != null && result.Successful && !string.IsNullOrEmpty(result.Token))
            {
                await _localStorage.SetItemAsync("authToken", result.Token);
                if (result.RefreshToken != null)
                {
                    await _localStorage.SetItemAsync("refreshToken", result.RefreshToken);
                }
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.Token);
            }

            return result ?? new LoginResponse { Successful = false, Error = "Invalid response from server" };
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("JavaScript interop"))
        {
            // This shouldn't happen during login as it's triggered by a user action,
            // but handling just in case
            return new LoginResponse { Successful = false, Error = "Cannot perform login during prerendering" };
        }
    }

    public async Task<RegistrationResponse> Register(RegistrationRequest registrationRequest)
    {
        var requestJson = JsonSerializer.Serialize(registrationRequest);
        var requestContent = new StringContent(requestJson, Encoding.UTF8, "application/json");
        
        var response = await _httpClient.PostAsync("api/Users/register", requestContent);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return new RegistrationResponse 
            { 
                Successful = false, 
                Errors = new[] { $"Error: {response.StatusCode} - {content}" } 
            };
        }

        var result = JsonSerializer.Deserialize<RegistrationResponse>(content, _jsonOptions);
        return result ?? new RegistrationResponse { Successful = false, Errors = new[] { "Invalid response from server" } };
    }    public async Task Logout()
    {
        // Check if we're running server-side (prerendering) before any JS interop
        if (OperatingSystem.IsBrowser())
        {
            await _localStorage.RemoveItemAsync("authToken");
            await _localStorage.RemoveItemAsync("refreshToken");
        }
        _httpClient.DefaultRequestHeaders.Authorization = null;
    }public async Task<UserInfo?> GetUserInfo()
    {
        try
        {
            // Check if we're running server-side (prerendering) before any JS interop
            if (!OperatingSystem.IsBrowser())
            {
                return null;
            }
            
            var token = await _localStorage.GetItemAsStringAsync("authToken");
            if (string.IsNullOrEmpty(token))
            {
                return null;
            }

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var userInfo = await _httpClient.GetFromJsonAsync<UserInfo>("api/Users/me");
            return userInfo;
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("JavaScript interop"))
        {
            // If JavaScript interop isn't available (prerendering), return null
            return null;
        }
        catch
        {
            return null;
        }
    }
}
