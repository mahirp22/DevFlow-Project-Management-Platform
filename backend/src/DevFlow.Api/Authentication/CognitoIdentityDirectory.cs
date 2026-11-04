using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DevFlow.Application.Identity;

namespace DevFlow.Api.Authentication;

// HTTP transport adapter: reads verified attributes from the configured provider, never from a browser DTO.
public sealed class CognitoIdentityDirectory(HttpClient client, CognitoSettings settings) : IIdentityDirectory
{
    public async Task<VerifiedIdentity?> GetVerifiedAsync(string accessToken, string subject, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, settings.Domain.TrimEnd('/') + "/oauth2/userInfo");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        try
        {
            using var response = await client.SendAsync(request, cancellationToken);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) return null;
            if (!response.IsSuccessStatusCode) throw new IdentityProviderUnavailableException();
            var data = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            if (data.ValueKind != JsonValueKind.Object) throw new IdentityProviderUnavailableException();
            string? Text(string key) => data.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            var verified = data.TryGetProperty("email_verified", out var flag)
                && (flag.ValueKind == JsonValueKind.True || (flag.ValueKind == JsonValueKind.String && flag.GetString() == "true"));
            var email = Text("email");
            if (Text("sub") != subject || !verified || string.IsNullOrWhiteSpace(email) || email.Length > 320)
                return null;
            var name = Text("name")?.Trim();
            if (string.IsNullOrWhiteSpace(name)) name = "DevFlow member";
            if (name.Length > 100) name = name[..100];
            return new VerifiedIdentity(subject, email, name);
        }
        catch (HttpRequestException) { throw new IdentityProviderUnavailableException(); }
        catch (JsonException) { throw new IdentityProviderUnavailableException(); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new IdentityProviderUnavailableException(); }
    }
}
