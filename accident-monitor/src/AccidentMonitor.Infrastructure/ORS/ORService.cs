using System.Text.Json;
using AccidentMonitor.Application.Common.Exceptions;
using AccidentMonitor.Application.Common.Interfaces;
using AccidentMonitor.Application.Converter;
using AccidentMonitor.Application.ORService.Queries.GetDirectionAdvanced.Dtos;
using AccidentMonitor.Application.ORService.Queries.GetDirections.Dtos;

namespace AccidentMonitor.Infrastructure.ORS;
public class ORService : IORService
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUri;
    private readonly JsonSerializerOptions? _jsonSerializerOptions;
    public ORService(ORSConfiguration options)
    {
        _httpClient = new HttpClient();
        _baseUri = $"{options.Uri}".TrimEnd('/') + $":{options.Port}{options.BasePath}";
        _jsonSerializerOptions = new JsonSerializerOptions
        {
            Converters =
            {
                new CoordinateJsonConverter()
            },
            WriteIndented = false
        };
    }

    public async Task<TResponse> GetStatus<TResponse>()
    {
        _ = new HttpResponseMessage();
        HttpResponseMessage? response;
        try
        {
            response = await _httpClient.GetAsync($"{_baseUri}/status");
        }
        catch
        {
            throw new ServicesUnavailableException("ORService is currently not available.");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InternalServerErrorException();
        }

        var responseContent = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<TResponse>(responseContent);

        return result!;
    }

    public async Task<TResponse> HealthCheck<TResponse>()
    {
        _ = new HttpResponseMessage();
        HttpResponseMessage? response;
        try
        {
            response = await _httpClient.GetAsync($"{_baseUri}/health");
        }
        catch
        {
            throw new ServicesUnavailableException("ORService is currently not available.");
        }
        if (!response.IsSuccessStatusCode)
        {
            throw new InternalServerErrorException();
        }

        var responseContent = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<TResponse>(responseContent);
        return result!;
    }

    public async Task<TResponse> GetRoutingDirectionAsync<TResponse>(string profile, GetDirectionRequestDto request)
    {
        var url = $"{_baseUri}/directions/{profile}" +
            $"?start={Uri.EscapeDataString(request.StartingCoordinate)}" +
            $"&end={Uri.EscapeDataString(request.DestinationCoordinate)}";
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, url);

        _ = new HttpResponseMessage();
        HttpResponseMessage? response;
        try
        {
            response = await _httpClient.SendAsync(requestMessage);
        }
        catch
        {
            throw new ServicesUnavailableException("ORService is currently not available.");
        }
        if (!response.IsSuccessStatusCode)
        {
            throw new InternalServerErrorException();
        }

        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new ServicesUnavailableException(url);
        }
        var result = JsonSerializer.Deserialize<TResponse>(responseContent);
        return result!;
    }

    public async Task<TResponse> GetAdvancedRoutingDirectionAsync<TResponse>(string profile, GetDirectionAdvanceRequestDto request)
    {
        var url = $"{_baseUri}/directions/{profile}";
        var requestBody = JsonSerializer.Serialize(request, _jsonSerializerOptions);
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json")
        };
        _ = new HttpResponseMessage();
        HttpResponseMessage? response;
        try
        {
            response = await _httpClient.SendAsync(requestMessage);
        }
        catch
        {
            throw new ServicesUnavailableException("ORService is currently not available.");
        }
        if (!response.IsSuccessStatusCode)
        {
            throw new InternalServerErrorException();
        }

        var responseContent = await response.Content.ReadAsStringAsync(); 

        switch (response.StatusCode)
        {
            case System.Net.HttpStatusCode.BadRequest:
                throw new BadRequestException(responseContent + '\n' + requestBody);
            case System.Net.HttpStatusCode.InternalServerError:
                throw new ServicesUnavailableException(url);
        }

        var result = JsonSerializer.Deserialize<TResponse>(responseContent, _jsonSerializerOptions);
        return result!;
    }
}
