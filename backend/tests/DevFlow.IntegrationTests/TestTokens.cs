using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace DevFlow.IntegrationTests;

internal static class TestTokens
{
    public const string Issuer = "https://cognito-idp.us-east-1.amazonaws.com/us-east-1_test";
    public const string ClientId = "devflow-test-client";
    public static readonly RsaSecurityKey Key = new(RSA.Create(2048)) { KeyId = "test-only-key" };

    public static string Create(string subject, string? failure = null)
    {
        var claims = new List<Claim>
        {
            new("sub", subject),
            new("token_use", failure == "id" ? "id" : "access"),
            new("client_id", failure == "client" ? "another-client" : ClientId),
            new("scope", failure == "scope" ? "email" : "openid email profile"),
            new("cognito:groups", "Owner"), // Deliberately ignored by tenant authorization.
        };
        SecurityKey key = failure == "signature" ? new RsaSecurityKey(RSA.Create(2048)) : Key;
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: failure == "issuer" ? "https://another-issuer.invalid" : Issuer,
            claims: claims,
            notBefore: failure == "future" ? now.AddMinutes(5) : now.AddMinutes(-10),
            expires: failure == "expired" ? now.AddMinutes(-5) : now.AddMinutes(10),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.RsaSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
