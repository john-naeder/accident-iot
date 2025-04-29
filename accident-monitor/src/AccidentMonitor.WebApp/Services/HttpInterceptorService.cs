using System.Net.Http.Headers;
using AccidentMonitor.WebApp.Services;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;

namespace AccidentMonitor.WebApp.Services;

public class HttpInterceptorService
{
    private readonly ILocalStorageService _localStorage;
    private readonly AuthenticationStateProvider _authStateProvider;
    private readonly HttpClient _httpClient;

    public HttpInterceptorService(
        ILocalStorageService localStorage,
        AuthenticationStateProvider authStateProvider,
        HttpClient httpClient)
    {
        _localStorage = localStorage;
        _authStateProvider = authStateProvider;
        _httpClient = httpClient;
    }

    public async Task<bool> AddTokenToHeader()
    {
        var token = await _localStorage.GetItemAsync<string>("authToken");
        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return true;
    }

    public async Task RemoveAuthenticationToken()
    {
        await _localStorage.RemoveItemAsync("authToken");
        await _localStorage.RemoveItemAsync("refreshToken");
        _httpClient.DefaultRequestHeaders.Authorization = null;
        
        ((CustomAuthenticationStateProvider)_authStateProvider).NotifyAuthenticationStateChanged();
    }
}
