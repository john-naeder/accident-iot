using System.Security.Claims;
using System.Text.Json;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using System.Net.Http.Headers;

namespace AccidentMonitor.WebApp.Services;

public class CustomAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly ILocalStorageService _localStorage;
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public CustomAuthenticationStateProvider(
        ILocalStorageService localStorage,
        HttpClient httpClient)
    {
        _localStorage = localStorage;
        _httpClient = httpClient;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var anonymousState = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));

        try
        {
            // Check if we're running server-side (prerendering) before any JS interop
            if (!OperatingSystem.IsBrowser())
            {
                // Return anonymous state during prerendering
                return anonymousState;
            }

            var savedToken = await _localStorage.GetItemAsync<string>("authToken");

            if (string.IsNullOrWhiteSpace(savedToken))
            {
                return anonymousState;
            }

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", savedToken);

            var authenticatedUser = new ClaimsPrincipal(
                new ClaimsIdentity(ParseClaimsFromJwt(savedToken), "jwt"));

            var response = await _httpClient.GetAsync("api/Users/validate-token");
            if (!response.IsSuccessStatusCode)
            {
                await _localStorage.RemoveItemAsync("authToken");
                return anonymousState;
            }

            return new AuthenticationState(authenticatedUser);
        }
        catch
        {
            // If we're in the browser, clean up the token
            if (OperatingSystem.IsBrowser())
            {
                await _localStorage.RemoveItemAsync("authToken");
            }
            return anonymousState;
        }
    }

    public void NotifyAuthenticationStateChanged()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    private static IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
    {
        var claims = new List<Claim>();
        var payload = jwt.Split('.')[1];
        var jsonBytes = ParseBase64WithoutPadding(payload);
        var keyValuePairs = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonBytes);

        if (keyValuePairs != null)
        {
            claims.AddRange(keyValuePairs.Select(kvp => new Claim(kvp.Key, kvp.Value.ToString() ?? string.Empty)));
        }

        return claims;
    }

    private static byte[] ParseBase64WithoutPadding(string base64)
    {
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }
        return Convert.FromBase64String(base64);
    }
}
