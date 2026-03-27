using Microsoft.EntityFrameworkCore;
using Minit.Api.Data;
using Minit.Api.Domain;
using Minit.Api.Dtos;
using Minit.Api.Services;

namespace Minit.Api.Endpoints;

public static class ContactsEndpoints
{
    public static RouteGroupBuilder MapContactsEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/contacts")
            .WithTags("Contacts");

        group.MapPost("/add", AddContactAsync)
            .WithName("AddContact")
            .WithSummary("Add contact by code")
            .Produces<AddContactResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/{ownerUserId:guid}", GetContactsAsync)
            .WithName("GetContacts")
            .WithSummary("List contacts for an owner")
            .Produces<List<ContactItemResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<IResult> AddContactAsync(
        AddContactRequest request,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var owner = await dbContext.Users
            .SingleOrDefaultAsync(x => x.Id == request.OwnerUserId && x.IsActive, cancellationToken);
        if (owner is null)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Owner not found",
                detail: "Owner user does not exist or is inactive.",
                extensions: new Dictionary<string, object?> { ["errorCode"] = "owner_not_found" });
        }

        var normalizedCode = request.ContactCode.Trim().ToUpperInvariant();
        var contactUser = await dbContext.Users
            .SingleOrDefaultAsync(x => x.Code == normalizedCode && x.IsActive, cancellationToken);
        if (contactUser is null)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Contact not found",
                detail: "No user found for the provided code.",
                extensions: new Dictionary<string, object?> { ["errorCode"] = "contact_not_found" });
        }

        if (contactUser.Id == owner.Id)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid contact",
                detail: "Users cannot add themselves as contact.",
                extensions: new Dictionary<string, object?> { ["errorCode"] = "cannot_add_self" });
        }

        var alreadyExists = await dbContext.Contacts
            .AnyAsync(x => x.OwnerUserId == owner.Id && x.ContactUserId == contactUser.Id, cancellationToken);
        if (alreadyExists)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Contact already exists",
                detail: "This contact is already in the owner's contact list.",
                extensions: new Dictionary<string, object?> { ["errorCode"] = "contact_exists" });
        }

        var contact = new Contact
        {
            Id = Guid.NewGuid(),
            OwnerUserId = owner.Id,
            ContactUserId = contactUser.Id,
            CreatedAt = DateTimeProvider.UtcNow
        };

        dbContext.Contacts.Add(contact);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/contacts/{owner.Id}", new AddContactResponse(
            contact.Id,
            owner.Id,
            contactUser.Id,
            contactUser.DisplayName,
            contact.CreatedAt));
    }

    private static async Task<IResult> GetContactsAsync(
        Guid ownerUserId,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var ownerExists = await dbContext.Users
            .AnyAsync(x => x.Id == ownerUserId && x.IsActive, cancellationToken);
        if (!ownerExists)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Owner not found",
                detail: "Owner user does not exist or is inactive.",
                extensions: new Dictionary<string, object?> { ["errorCode"] = "owner_not_found" });
        }

        var contacts = await dbContext.Contacts
            .AsNoTracking()
            .Where(x => x.OwnerUserId == ownerUserId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new ContactItemResponse(
                x.ContactUserId,
                x.ContactUser!.DisplayName,
                x.ContactUser.Code,
                x.CreatedAt))
            .ToListAsync(cancellationToken);

        return Results.Ok(contacts);
    }
}
