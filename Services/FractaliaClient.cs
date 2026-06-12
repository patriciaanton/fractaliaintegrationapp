using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FractaliaIntegrationApp.Services;

public interface IFractaliaClient
{
    Task LoginAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PartnerDto>?> GetPartnersAsync(CancellationToken cancellationToken = default);
}

public sealed class FractaliaClient : IFractaliaClient
{
    public const string DefaultBaseUrl = "http://1086se.com:8080/";

    private const string ApiKeyHeaderName = "x-api-key";
    private const string ApiKeyValue = "fractalia-static-api-key";
    private const string LoginUsername = "fractalia-admin";
    private const string LoginPassword = "fractalia-pass-2026";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _loginLock = new(1, 1);
    private string? _jwtToken;

    public FractaliaClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task LoginAsync(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/login");
        request.Headers.Add(ApiKeyHeaderName, ApiKeyValue);
        request.Content = JsonContent.Create(
            new LoginRequest(LoginUsername, LoginPassword),
            options: JsonOptions);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("La respuesta de login no contiene datos.");

        var token = loginResponse.GetJwtToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("La respuesta de login no incluye un token JWT válido.");
        }

        _jwtToken = token;
    }

    public async Task<IReadOnlyList<PartnerDto>?> GetPartnersAsync(CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticatedAsync(cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, "api/Partners");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _jwtToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<PartnerDto>>(JsonOptions, cancellationToken);
    }

    private async Task EnsureAuthenticatedAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_jwtToken))
        {
            return;
        }

        await _loginLock.WaitAsync(cancellationToken);
        try
        {
            if (string.IsNullOrWhiteSpace(_jwtToken))
            {
                await LoginAsync(cancellationToken);
            }
        }
        finally
        {
            _loginLock.Release();
        }
    }
}

public sealed record LoginRequest(
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("password")] string Password);

public sealed record LoginResponse(
    [property: JsonPropertyName("token")] string? Token,
    [property: JsonPropertyName("accessToken")] string? AccessToken)
{
    public string GetJwtToken() =>
        !string.IsNullOrWhiteSpace(Token) ? Token : AccessToken ?? string.Empty;
}

public sealed record PartnerDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("code")] string? Code);
