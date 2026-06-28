using System.Net;
using System.Net.Http.Json;
using Corbel.Api.Tests.Fixtures;
using Corbel.Common.Pagination;
using Corbel.Features.Notes;
using Corbel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Corbel.Api.Tests.Integration;

/// <summary>End-to-end notes slice over the real host + Postgres: ownership scoping, the CRUD round-trip, and paging shape.</summary>
[Collection(CorbelCollection.Name)]
public sealed class NotesApiTests(CorbelFixture fixture) : IAsyncLifetime
{
    private readonly CorbelFixture _fixture = fixture;

    public async ValueTask InitializeAsync() => await _fixture.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Unauthenticated_request_is_rejected()
    {
        using var client = _fixture.Api.CreateApiClient();

        var response = await client.GetAsync("/api/notes", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Full_crud_round_trip()
    {
        using var client = await _fixture.Api.CreateAuthenticatedClientAsync("crud@corbel.test", "Passw0rd!");

        // Create
        var createResponse = await client.PostAsJsonAsync("/api/notes", new { title = "First", content = "hello" }, cancellationToken: TestContext.Current.CancellationToken);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await createResponse.ReadJsonAsync<NoteResponse>();
        created.Title.ShouldBe("First");
        created.Content.ShouldBe("hello");
        created.IsArchived.ShouldBeFalse();
        created.Id.ShouldNotBe(Guid.Empty);

        // Get
        var fetched = await (await client.GetAsync($"/api/notes/{created.Id}", TestContext.Current.CancellationToken)).ReadJsonAsync<NoteResponse>();
        fetched.Id.ShouldBe(created.Id);

        // List
        var list = await (await client.GetAsync("/api/notes", TestContext.Current.CancellationToken)).ReadJsonAsync<PagedResult<NoteResponse>>();
        list.TotalCount.ShouldBe(1);
        list.Items.ShouldContain(n => n.Id == created.Id);

        // Update
        var updateResponse = await client.PutAsJsonAsync($"/api/notes/{created.Id}", new { title = "Renamed", content = "changed" }, cancellationToken: TestContext.Current.CancellationToken);
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await updateResponse.ReadJsonAsync<NoteResponse>();
        updated.Title.ShouldBe("Renamed");
        updated.Content.ShouldBe("changed");

        // Archive
        var archived = await (await client.PostAsync($"/api/notes/{created.Id}/archive", content: null, cancellationToken: TestContext.Current.CancellationToken))
            .ReadJsonAsync<NoteResponse>();
        archived.IsArchived.ShouldBeTrue();

        // Archiving again violates the domain invariant → 422 with the stable error code the TS client keys on.
        var archiveAgain = await client.PostAsync($"/api/notes/{created.Id}/archive", content: null, cancellationToken: TestContext.Current.CancellationToken);
        archiveAgain.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await archiveAgain.ReadErrorCodeAsync()).ShouldBe("note.already_archived");

        // Delete (soft delete) then it disappears from the owner-scoped query.
        var deleteResponse = await client.DeleteAsync($"/api/notes/{created.Id}", TestContext.Current.CancellationToken);
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var afterDelete = await client.GetAsync($"/api/notes/{created.Id}", TestContext.Current.CancellationToken);
        afterDelete.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task User_cannot_reach_another_users_note_returns_404_not_403()
    {
        using var alice = await _fixture.Api.CreateAuthenticatedClientAsync("alice@corbel.test", "Passw0rd!");
        using var bob = await _fixture.Api.CreateAuthenticatedClientAsync("bob@corbel.test", "Passw0rd!");

        var created = await (await alice.PostAsJsonAsync("/api/notes", new { title = "Alice secret", content = "x" }, cancellationToken: TestContext.Current.CancellationToken))
            .ReadJsonAsync<NoteResponse>();

        // BOLA defense: another user's note is indistinguishable from one that doesn't exist (404, not 403).
        var bobGet = await bob.GetAsync($"/api/notes/{created.Id}", TestContext.Current.CancellationToken);
        bobGet.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await bobGet.ReadErrorCodeAsync()).ShouldBe("note.not_found");

        var bobUpdate = await bob.PutAsJsonAsync($"/api/notes/{created.Id}", new { title = "hijack", content = "y" }, cancellationToken: TestContext.Current.CancellationToken);
        bobUpdate.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var bobDelete = await bob.DeleteAsync($"/api/notes/{created.Id}", TestContext.Current.CancellationToken);
        bobDelete.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // Bob's own list never sees Alice's note.
        var bobList = await (await bob.GetAsync("/api/notes", TestContext.Current.CancellationToken)).ReadJsonAsync<PagedResult<NoteResponse>>();
        bobList.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task List_returns_paged_envelope_shape()
    {
        using var client = await _fixture.Api.CreateAuthenticatedClientAsync("pager@corbel.test", "Passw0rd!");

        for (var i = 0; i < 3; i++)
            (await client.PostAsJsonAsync("/api/notes", new { title = $"Note {i}", content = "c" }, cancellationToken: TestContext.Current.CancellationToken))
                .EnsureSuccessStatusCode();

        var page = await (await client.GetAsync("/api/notes?page=1&pageSize=2", TestContext.Current.CancellationToken))
            .ReadJsonAsync<PagedResult<NoteResponse>>();

        page.Page.ShouldBe(1);
        page.PageSize.ShouldBe(2);
        page.TotalCount.ShouldBe(3);
        page.Items.Count.ShouldBe(2);
        page.TotalPages.ShouldBe(2);
        page.HasNext.ShouldBeTrue();
        page.HasPrevious.ShouldBeFalse();
    }

    [Fact]
    public async Task Deleting_a_note_soft_deletes_it_and_preserves_audit()
    {
        using var client = await _fixture.Api.CreateAuthenticatedClientAsync("softdelete@corbel.test", "Passw0rd!");

        var created = await (await client.PostAsJsonAsync("/api/notes", new { title = "Doomed", content = "x" }, cancellationToken: TestContext.Current.CancellationToken))
            .ReadJsonAsync<NoteResponse>();

        (await client.DeleteAsync($"/api/notes/{created.Id}", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // The row is hidden from normal queries but still present (soft delete), with its audit fields intact.
        using var scope = _fixture.Api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.Notes.IgnoreQueryFilters()
            .FirstAsync(n => n.Id == created.Id, TestContext.Current.CancellationToken);

        row.IsDeleted.ShouldBeTrue();
        row.DeletedAtUtc.ShouldNotBeNull();
        row.CreatedAtUtc.ShouldNotBe(default);
        row.CreatedBy.ShouldNotBeNull();
    }

    [Fact]
    public async Task List_clamps_page_size_to_one_hundred_and_defaults_to_twenty()
    {
        using var client = await _fixture.Api.CreateAuthenticatedClientAsync("clamp@corbel.test", "Passw0rd!");

        (await client.PostAsJsonAsync("/api/notes", new { title = "Only", content = "c" }, cancellationToken: TestContext.Current.CancellationToken))
            .EnsureSuccessStatusCode();

        // No pageSize → default 20.
        var defaulted = await (await client.GetAsync("/api/notes", TestContext.Current.CancellationToken)).ReadJsonAsync<PagedResult<NoteResponse>>();
        defaulted.PageSize.ShouldBe(20);

        // Above the cap → clamped down to 100.
        var tooLarge = await (await client.GetAsync("/api/notes?pageSize=1000", TestContext.Current.CancellationToken)).ReadJsonAsync<PagedResult<NoteResponse>>();
        tooLarge.PageSize.ShouldBe(100);

        // Below the floor → clamped up to 1.
        var tooSmall = await (await client.GetAsync("/api/notes?pageSize=0", TestContext.Current.CancellationToken)).ReadJsonAsync<PagedResult<NoteResponse>>();
        tooSmall.PageSize.ShouldBe(1);

        // A non-positive page falls back to the first page.
        var badPage = await (await client.GetAsync("/api/notes?page=0", TestContext.Current.CancellationToken)).ReadJsonAsync<PagedResult<NoteResponse>>();
        badPage.Page.ShouldBe(1);
    }

    [Fact]
    public async Task List_search_filters_by_title_case_insensitively()
    {
        using var client = await _fixture.Api.CreateAuthenticatedClientAsync("search@corbel.test", "Passw0rd!");

        foreach (var title in new[] { "Apple pie", "Banana bread", "apple tart" })
            (await client.PostAsJsonAsync("/api/notes", new { title, content = "c" }, cancellationToken: TestContext.Current.CancellationToken))
                .EnsureSuccessStatusCode();

        // Case-insensitive title match: "Apple pie" and "apple tart", but not "Banana bread".
        var apples = await (await client.GetAsync("/api/notes?search=apple", TestContext.Current.CancellationToken)).ReadJsonAsync<PagedResult<NoteResponse>>();
        apples.TotalCount.ShouldBe(2);
        apples.Items.ShouldAllBe(n => n.Title.Contains("apple", StringComparison.OrdinalIgnoreCase));

        var bananas = await (await client.GetAsync("/api/notes?search=banana", TestContext.Current.CancellationToken)).ReadJsonAsync<PagedResult<NoteResponse>>();
        bananas.TotalCount.ShouldBe(1);
        bananas.Items.Single().Title.ShouldBe("Banana bread");
    }

    [Fact]
    public async Task List_search_also_matches_note_content()
    {
        using var client = await _fixture.Api.CreateAuthenticatedClientAsync("contentsearch@corbel.test", "Passw0rd!");

        (await client.PostAsJsonAsync("/api/notes", new { title = "Groceries", content = "remember the pineapple" }, cancellationToken: TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/api/notes", new { title = "Chores", content = "mow the lawn" }, cancellationToken: TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();

        // The generically-named `search` term matches Title OR Content — here it appears only in the content.
        var result = await (await client.GetAsync("/api/notes?search=pineapple", TestContext.Current.CancellationToken)).ReadJsonAsync<PagedResult<NoteResponse>>();
        result.TotalCount.ShouldBe(1);
        result.Items.Single().Title.ShouldBe("Groceries");
    }

    [Fact]
    public async Task Concurrent_updates_to_the_same_note_raise_an_optimistic_concurrency_conflict()
    {
        using var client = await _fixture.Api.CreateAuthenticatedClientAsync("concurrency@corbel.test", "Passw0rd!");

        var created = await (await client.PostAsJsonAsync("/api/notes", new { title = "Racy", content = "v1" }, cancellationToken: TestContext.Current.CancellationToken))
            .ReadJsonAsync<NoteResponse>();

        // Load the same row into two independent contexts, then save both. PostgreSQL's xmin token (see
        // NoteConfiguration) makes the second save's UPDATE … WHERE xmin = <stale> affect 0 rows, which EF surfaces
        // as DbUpdateConcurrencyException — the exact failure GlobalExceptionHandler maps to 409. Drop the xmin
        // Property mapping and this goes red instead of silently last-writer-wins.
        using var scopeA = _fixture.Api.Services.CreateScope();
        using var scopeB = _fixture.Api.Services.CreateScope();
        var dbA = scopeA.ServiceProvider.GetRequiredService<AppDbContext>();
        var dbB = scopeB.ServiceProvider.GetRequiredService<AppDbContext>();

        var noteA = await dbA.Notes.FirstAsync(n => n.Id == created.Id, TestContext.Current.CancellationToken);
        var noteB = await dbB.Notes.FirstAsync(n => n.Id == created.Id, TestContext.Current.CancellationToken);

        noteA.Edit("Winner", "v2");
        await dbA.SaveChangesAsync(TestContext.Current.CancellationToken);

        noteB.Edit("Loser", "v3");
        await Should.ThrowAsync<DbUpdateConcurrencyException>(
            () => dbB.SaveChangesAsync(TestContext.Current.CancellationToken));
    }
}
