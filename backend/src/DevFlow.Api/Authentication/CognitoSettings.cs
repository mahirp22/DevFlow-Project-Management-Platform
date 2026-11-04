namespace DevFlow.Api.Authentication;

public sealed class CognitoSettings
{
    public string Authority { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string Domain { get; set; } = "";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Authority)
        && !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(Domain);

    public void Validate(bool development)
    {
        if (!IsConfigured)
        {
            if (!development || Authority.Length + ClientId.Length + Domain.Length > 0)
                throw new InvalidOperationException("Configure Cognito:Authority, ClientId and Domain together.");
            return; // Local shell/health only; there are no signing keys and authentication fails closed.
        }
        if (!IsHttpsUrl(Authority) || !IsHttpsUrl(Domain)
            || new Uri(Domain).AbsolutePath != "/" || new Uri(Authority).AbsolutePath == "/")
            throw new InvalidOperationException("Cognito Authority must be an HTTPS issuer URL; Domain must be an HTTPS origin.");
    }

    private static bool IsHttpsUrl(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo)
        && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment);
}
