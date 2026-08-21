// tests/ChurchProjection.Api.Tests/AccessTests.cs
//
// SYS-SEC-01/02/03 and INT-09/10. The pair gate is the only thing between the
// sanctuary Wi-Fi and the screen behind the pulpit, so it is tested from the
// outside, over HTTP, the way an attacker would meet it.

using System.Net;
using System.Net.Http.Json;

namespace ChurchProjection.Api.Tests;

public class AccessTests(ProjectionAppFactory factory) : IClassFixture<ProjectionAppFactory>
{
    [Fact]
    public async Task SYS_SEC_01_an_unpaired_request_is_refused()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/translations", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SYS_SEC_02_the_right_pin_opens_the_gate()
    {
        var client = factory.CreateClient();

        var paired = await client.PostAsJsonAsync(
            "/api/pair",
            new { pin = ProjectionAppFactory.TestPin },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, paired.StatusCode);
        Assert.Contains(paired.Headers.GetValues("set-cookie"), value => value.StartsWith("pair="));

        var cookie = paired.Headers.GetValues("set-cookie").First().Split(';')[0];
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/translations");
        request.Headers.Add("Cookie", cookie);

        var listed = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
    }

    [Fact]
    public async Task SYS_SEC_03_the_wrong_pin_is_refused()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/pair", new { pin = "000000" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain(response.Headers, header => header.Key.Equals("set-cookie", StringComparison.OrdinalIgnoreCase));
    }
}
