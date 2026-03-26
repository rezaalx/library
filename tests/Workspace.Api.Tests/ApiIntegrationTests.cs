using System.Net;
using System.Net.Http.Json;
using Workspace.Api.Tests.Infrastructure;
using Workspace.Dtos.Admin;
using Workspace.Dtos.Calls;
using Workspace.Dtos.Contacts;
using Workspace.Dtos.Usage;
using AdminUsageAdjustmentRequest = Workspace.Dtos.Admin.UsageAdjustmentRequest;
using Workspace.Dtos.Users;

namespace Workspace.Api.Tests;

public sealed class ApiIntegrationTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    [Fact]
    public async Task Register_ByCode_AddContact_ListContacts_ShouldSucceed()
    {
        using var client = factory.CreateClient();

        var userA = await RegisterUserAsync(client, "Alice");
        var userB = await RegisterUserAsync(client, "Bob");

        Assert.Equal(8, userA.Code.Length);
        Assert.Equal(8, userB.Code.Length);
        Assert.NotEqual(userA.UserId, userB.UserId);

        var byCodeResponse = await client.GetAsync($"/api/users/by-code/{userB.Code}");
        byCodeResponse.AssertStatus(HttpStatusCode.OK);
        var byCode = await byCodeResponse.ReadRequiredAsync<UserByCodeResponse>();
        Assert.Equal(userB.UserId, byCode.UserId);
        Assert.Equal("Bob", byCode.DisplayName);

        var addContact = await client.PostAsJsonAsync("/api/contacts/add", new AddContactRequest
        {
            OwnerUserId = userA.UserId,
            ContactCode = userB.Code
        });
        addContact.AssertStatus(HttpStatusCode.Created);

        var listContacts = await client.GetAsync($"/api/contacts/{userA.UserId}");
        listContacts.AssertStatus(HttpStatusCode.OK);
        var contacts = await listContacts.ReadRequiredAsync<List<ContactItemResponse>>();
        Assert.Single(contacts);
        Assert.Equal(userB.UserId, contacts[0].UserId);
        Assert.Equal("Bob", contacts[0].DisplayName);
    }

    [Fact]
    public async Task Start_Join_End_Call_ShouldBillCreatorAndUpdateUsage()
    {
        using var client = factory.CreateClient();

        var creator = await RegisterUserAsync(client, "CallCreator");
        var callee = await RegisterUserAsync(client, "CallCallee");

        var start = await client.PostAsJsonAsync("/api/calls/start", new StartCallRequest
        {
            CreatedByUserId = creator.UserId,
            CalleeUserId = callee.UserId,
            Provider = "internal",
            ProviderRoomId = "room-123"
        });
        start.AssertStatus(HttpStatusCode.OK);
        var started = await start.ReadRequiredAsync<StartCallResponse>();

        var join = await client.PostAsJsonAsync($"/api/calls/{started.CallId}/join", new JoinCallRequest
        {
            UserId = callee.UserId
        });
        join.AssertStatus(HttpStatusCode.OK);

        await Task.Delay(TimeSpan.FromSeconds(2));

        var end = await client.PostAsJsonAsync($"/api/calls/{started.CallId}/end", new EndCallRequest
        {
            EndedByUserId = creator.UserId
        });
        end.AssertStatus(HttpStatusCode.OK);
        var ended = await end.ReadRequiredAsync<EndCallResponse>();
        Assert.True(ended.BilledSeconds > 0);
        Assert.True(ended.UsedSeconds >= ended.BilledSeconds);

        var month = DateTime.UtcNow.Year * 100 + DateTime.UtcNow.Month;
        var usage = await client.GetAsync($"/api/usage/{creator.UserId}/month/{month}");
        usage.AssertStatus(HttpStatusCode.OK);
        var usageBody = await usage.ReadRequiredAsync<UsageResponse>();
        Assert.True(usageBody.UsedSeconds > 0);
    }

    [Fact]
    public async Task QuotaExceeded_ShouldReturnForbiddenOnCallStart()
    {
        using var client = factory.CreateClient();

        var creator = await RegisterUserAsync(client, "QuotaCreator");
        var callee = await RegisterUserAsync(client, "QuotaCallee");
        var month = DateTime.UtcNow.Year * 100 + DateTime.UtcNow.Month;

        client.DefaultRequestHeaders.Add("X-Admin-Key", ApiTestFactory.TestAdminKey);

        var setLimit = await client.PatchAsJsonAsync(
            $"/api/admin/users/{creator.UserId}/limit",
            new UpdateUserLimitRequest(1, null));
        setLimit.AssertStatus(HttpStatusCode.OK);

        var adjustment = await client.PostAsJsonAsync("/api/admin/usage-adjustments", new AdminUsageAdjustmentRequest(
            creator.UserId,
            month,
            5,
            "test-over-quota"));
        adjustment.AssertStatus(HttpStatusCode.OK);

        var start = await client.PostAsJsonAsync("/api/calls/start", new StartCallRequest
        {
            CreatedByUserId = creator.UserId,
            CalleeUserId = callee.UserId
        });
        start.AssertStatus(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdminEndpoints_ShouldRequireApiKey()
    {
        using var client = factory.CreateClient();
        var user = await RegisterUserAsync(client, "AdminTarget");

        var noKey = await client.PatchAsJsonAsync(
            $"/api/admin/users/{user.UserId}/limit",
            new UpdateUserLimitRequest(1200, null));
        noKey.AssertStatus(HttpStatusCode.Unauthorized);

        client.DefaultRequestHeaders.Add("X-Admin-Key", ApiTestFactory.TestAdminKey);

        var withKey = await client.PatchAsJsonAsync(
            $"/api/admin/users/{user.UserId}/limit",
            new UpdateUserLimitRequest(1200, null));
        withKey.AssertStatus(HttpStatusCode.OK);
        var updated = await withKey.ReadRequiredAsync<UpdateUserLimitResponse>();
        Assert.Equal(1200, updated.MonthlyLimitSeconds);
    }

    private static async Task<RegisterUserResponse> RegisterUserAsync(HttpClient client, string displayName)
    {
        var response = await client.PostAsJsonAsync("/api/users/register", new RegisterUserRequest
        {
            DisplayName = displayName
        });
        response.AssertStatus(HttpStatusCode.Created);
        return await response.ReadRequiredAsync<RegisterUserResponse>();
    }
}
