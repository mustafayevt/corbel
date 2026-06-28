using Corbel.Domain.Entities;
using Corbel.Domain.Exceptions;
using Shouldly;
using Xunit;

namespace Corbel.Api.Tests.Unit;

/// <summary>Behavior of the rich <see cref="Note"/> aggregate — invariants are enforced in the domain, not the handler.</summary>
public sealed class NoteTests
{
    private static readonly Guid Owner = Guid.CreateVersion7();

    [Fact]
    public void Create_trims_title_and_sets_defaults()
    {
        var note = Note.Create("  My note  ", "body", Owner);

        note.Title.ShouldBe("My note");
        note.Content.ShouldBe("body");
        note.OwnerId.ShouldBe(Owner);
        note.IsArchived.ShouldBeFalse();
        note.Id.ShouldNotBe(Guid.Empty);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_with_blank_title_throws(string? title) =>
        // The blank-title path is a 400 via FluentValidation in normal flow; the domain guard is defensive.
        Should.Throw<ArgumentException>(() => Note.Create(title!, "body", Owner));

    [Fact]
    public void Create_with_null_content_becomes_empty()
    {
        var note = Note.Create("title", null, Owner);
        note.Content.ShouldBe(string.Empty);
    }

    [Fact]
    public void Edit_updates_title_and_content()
    {
        var note = Note.Create("title", "body", Owner);

        note.Edit("  new title ", "new body");

        note.Title.ShouldBe("new title");
        note.Content.ShouldBe("new body");
    }

    [Fact]
    public void Edit_with_blank_title_throws()
    {
        var note = Note.Create("title", "body", Owner);
        Should.Throw<ArgumentException>(() => note.Edit("   ", "body"));
    }

    [Fact]
    public void Archive_sets_the_flag()
    {
        var note = Note.Create("title", "body", Owner);

        note.Archive();

        note.IsArchived.ShouldBeTrue();
    }

    [Fact]
    public void Archive_twice_throws()
    {
        var note = Note.Create("title", "body", Owner);
        note.Archive();

        Should.Throw<NoteAlreadyArchivedException>(() => note.Archive());
    }
}
