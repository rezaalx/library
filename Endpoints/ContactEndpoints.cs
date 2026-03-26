using Microsoft.EntityFrameworkCore;
using Workspace.Data;
using Workspace.Domain;
using Workspace.Dtos.Contacts;

namespace Workspace.Endpoints;

public static class ContactEndpoints
{
    public static RouteGroupBuilder MapContactEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/contacts").WithTags("Contacts");

        group.MapPost("/add", AddContactAsync);
        group.MapGet("/{ownerUserId:guid}", GetContactsAsync);

        return group;
    }

    private static async Task<IResult> AddContactAsync(
        AddContactRequest request,
        AppDbContext db,
        CancellationToken ct)
    {
        var owner = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.OwnerUserId && x.IsActive, ct);
        if (owner is null)
        {
            return Results.Problem(
                title: "Owner user not found",
                detail: $"User '{request.OwnerUserId}' does not exist or is inactive.",
                statusCode: StatusCodes.Status404NotFound,
                type: "user_not_found");
        }

        var normalizedCode = request.ContactCode.ToUpperInvariant();
        var contactUser = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Code == normalizedCode && x.IsActive, ct);
        if (contactUser is null)
        {
            return Results.Problem(
                title: "Contact user not found",
                detail: $"No active user exists for code '{normalizedCode}'.",
                statusCode: StatusCodes.Status404NotFound,
                type: "contact_not_found");
        }

        if (contactUser.Id == request.OwnerUserId)
        {
            return Results.Problem(
                title: "Cannot add self",
                detail: "Users cannot add themselves as contacts.",
                statusCode: StatusCodes.Status400BadRequest,
                type: "self_contact");
        }

        var exists = await db.Contacts.AnyAsync(
            x => x.OwnerUserId == request.OwnerUserId && x.ContactUserId == contactUser.Id,
            ct);
        if (exists)
        {
            return Results.Problem(
                title: "Duplicate contact",
                detail: "This contact already exists.",
                statusCode: StatusCodes.Status409Conflict,
                type: "duplicate_contact");
        }

        var now = DateTime.UtcNow;
        var contact = new Contact
        {
            Id = Guid.NewGuid(),
            OwnerUserId = request.OwnerUserId,
            ContactUserId = contactUser.Id,
            CreatedAt = now
        };

        db.Contacts.Add(contact);
        await db.SaveChangesAsync(ct);

        return Results.Created(
            $"/api/contacts/{request.OwnerUserId}",
            new ContactItemResponse(contactUser.Id, contactUser.DisplayName, contactUser.Code, now));
    }

    private static async Task<IResult> GetContactsAsync(
        Guid ownerUserId,
        AppDbContext db,
        CancellationToken ct)
    {
        var ownerExists = await db.Users.AnyAsync(x => x.Id == ownerUserId && x.IsActive, ct);
        if (!ownerExists)
        {
            return Results.Problem(
                title: "Owner user not found",
                detail: $"User '{ownerUserId}' does not exist or is inactive.",
                statusCode: StatusCodes.Status404NotFound,
                type: "user_not_found");
        }

        var contacts = await db.Contacts
            .AsNoTracking()
            .Where(x => x.OwnerUserId == ownerUserId)
            .Join(
                db.Users.AsNoTracking(),
                contact => contact.ContactUserId,
                user => user.Id,
                (contact, user) => new { contact, user })
            .Where(x => x.user.IsActive)
            .OrderBy(x => x.user.DisplayName)
            .Select(x => new ContactItemResponse(
                x.user.Id,
                x.user.DisplayName,
                x.user.Code,
                x.contact.CreatedAt))
            .ToListAsync(ct);

        return Results.Ok(contacts);
    }
}
