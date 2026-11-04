using System.Net;
using System.Net.Http.Json;
using DevFlow.Api.Authentication;
using DevFlow.Application.Identity;

namespace DevFlow.IntegrationTests;

public sealed class IdentityDirectoryTests
{
    [Theory]
    [InlineData(false, "subject")]
    [InlineData(true, "different-subject")]
    public async Task Unverified_email_or_subject_mismatch_is_rejected(bool verified, string returnedSubject)
    {
        using var http = new HttpClient(new ReplyHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        { Content = JsonContent.Create(new { sub = returnedSubject, email = "member@example.invalid", email_verified = verified }) }));
        var directory = new CognitoIdentityDirectory(http, new CognitoSettings { Domain = "https://identity.example.invalid" });
        Assert.Null(await directory.GetVerifiedAsync("test-only-token", "subject", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Verified_attributes_are_retrieved_from_the_configured_provider()
    {
        using var http = new HttpClient(new ReplyHandler(request =>
        {
            Assert.Equal("https://identity.example.invalid/oauth2/userInfo", request.RequestUri!.AbsoluteUri);
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new {
                sub = "subject", email = "member@example.invalid", email_verified = true, name = "Member" }) };
        }));
        var directory = new CognitoIdentityDirectory(http, new CognitoSettings { Domain = "https://identity.example.invalid" });
        Assert.Equal(new VerifiedIdentity("subject", "member@example.invalid", "Member"),
            await directory.GetVerifiedAsync("test-only-token", "subject", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(500)]
    [InlineData(302)]
    public async Task Provider_errors_do_not_create_profiles(int status)
    {
        using var http = new HttpClient(new ReplyHandler(_ => new HttpResponseMessage((HttpStatusCode)status)));
        var directory = new CognitoIdentityDirectory(http, new CognitoSettings { Domain = "https://identity.example.invalid" });
        await Assert.ThrowsAsync<IdentityProviderUnavailableException>(() =>
            directory.GetVerifiedAsync("test-only-token", "subject", TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Missing_production_configuration_is_rejected()
    {
        Assert.Throws<InvalidOperationException>(() => new CognitoSettings().Validate(development: false));
        new CognitoSettings().Validate(development: true);
    }
}

internal sealed class ReplyHandler(Func<HttpRequestMessage, HttpResponseMessage> reply) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(reply(request));
}
