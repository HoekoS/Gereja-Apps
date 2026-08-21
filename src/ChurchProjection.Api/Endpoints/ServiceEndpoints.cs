using ChurchProjection.Api.Access;
using ChurchProjection.Application.Ports;
using ChurchProjection.Domain.Library;
using ChurchProjection.Domain.Services;

namespace ChurchProjection.Api.Endpoints;

public static class ServiceEndpoints
{
    public sealed record CreateServiceRequest(string Name, DateOnly ServiceDate);

    public sealed record PatchServiceRequest(string? Name, DateOnly? ServiceDate);

    public sealed record ItemRequest(string Kind, string Label, ItemRef Ref);

    public sealed record PatchItemRequest(string? Label, ItemRef? Ref);

    public sealed record ReorderRequest(IReadOnlyList<string> ItemIds);

    private static readonly string[] Kinds = ["bible", "song", "slide", "media", "countdown"];

    public static void MapServices(this WebApplication app)
    {
        var group = app.MapGroup("/api/services").RequirePair();

        group.MapGet("/", async (IServiceRepository services, CancellationToken ct) =>
            Results.Json(await services.ListAsync(ct)));

        group.MapPost("/", async (
            CreateServiceRequest body, IServiceRepository services, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.Name))
            {
                return ApiError.BadRequest("NAME_REQUIRED", "Give the service a name.");
            }

            var plan = new ServicePlan
            {
                Id = $"svc_{Guid.NewGuid():n}"[..12],
                Name = body.Name,
                ServiceDate = body.ServiceDate,
            };

            await services.SaveAsync(plan, ct);

            return Results.Created($"/api/services/{plan.Id.Value}", Describe(plan));
        });

        group.MapGet("/{id}", async (string id, IServiceRepository services, CancellationToken ct) =>
            await services.FindAsync(new ServiceId(id), ct) is { } plan
                ? Results.Json(Describe(plan))
                : NoSuchService());

        group.MapPatch("/{id}", async (
            string id, PatchServiceRequest body, IServiceRepository services, CancellationToken ct) =>
        {
            if (await services.FindAsync(new ServiceId(id), ct) is not { } plan)
            {
                return NoSuchService();
            }

            plan.Rename(body.Name ?? plan.Name, body.ServiceDate ?? plan.ServiceDate);
            await services.SaveAsync(plan, ct);

            return Results.Json(Describe(plan));
        });

        group.MapDelete("/{id}", async (string id, IServiceRepository services, CancellationToken ct) =>
        {
            await services.RemoveAsync(new ServiceId(id), ct);

            return Results.NoContent();
        });

        group.MapPost("/{id}/items", async (
            string id, ItemRequest body, IServiceRepository services, CancellationToken ct) =>
        {
            if (await services.FindAsync(new ServiceId(id), ct) is not { } plan)
            {
                return NoSuchService();
            }

            if (!Kinds.Contains(body.Kind))
            {
                return ApiError.BadRequest("UNKNOWN_KIND", $"'{body.Kind}' is not a kind of service item.");
            }

            var item = new ServiceItem
            {
                Id = $"itm_{Guid.NewGuid():n}"[..12],
                Kind = body.Kind,
                Label = body.Label,
                Ref = body.Ref,
            };

            plan.Append(item);
            await services.SaveAsync(plan, ct);

            return Results.Created($"/api/services/{id}/items/{item.Id}", Describe(item));
        });

        group.MapPatch("/{id}/items/{itemId}", async (
            string id, string itemId, PatchItemRequest body, IServiceRepository services, CancellationToken ct) =>
        {
            if (await services.FindAsync(new ServiceId(id), ct) is not { } plan)
            {
                return NoSuchService();
            }

            if (plan.Find(itemId) is not { } item)
            {
                return ApiError.NotFound("UNKNOWN_ITEM", "That item is not in this service.");
            }

            item.Update(body.Label ?? item.Label, body.Ref ?? item.Ref);
            await services.SaveAsync(plan, ct);

            return Results.Json(Describe(item));
        });

        group.MapDelete("/{id}/items/{itemId}", async (
            string id, string itemId, IServiceRepository services, CancellationToken ct) =>
        {
            if (await services.FindAsync(new ServiceId(id), ct) is not { } plan)
            {
                return NoSuchService();
            }

            // FR-SVC-07: removing an item removes the item. The song it points
            // at stays in the library, which is what song-still-exists.bru
            // checks immediately afterwards.
            plan.Remove(itemId);
            await services.SaveAsync(plan, ct);

            // The updated order, not 204: delete-item.bru reads the renumbered
            // items straight out of this response.
            return Results.Json(Describe(plan));
        });

        group.MapPost("/{id}/items/reorder", async (
            string id, ReorderRequest body, IServiceRepository services, CancellationToken ct) =>
        {
            if (await services.FindAsync(new ServiceId(id), ct) is not { } plan)
            {
                return NoSuchService();
            }

            if (!plan.Reorder(body.ItemIds))
            {
                // The aggregate refused and changed nothing, so there is
                // nothing to roll back here.
                return ApiError.BadRequest(
                    "INCOMPLETE_ORDER", "The new order must list every item in the service exactly once.");
            }

            await services.SaveAsync(plan, ct);

            return Results.Json(Describe(plan));
        });
    }

    private static IResult NoSuchService() =>
        ApiError.NotFound("SERVICE_NOT_FOUND", "That service is not saved on this machine.");

    private static object Describe(ServicePlan plan) => new
    {
        id = plan.Id.Value,
        name = plan.Name,
        serviceDate = plan.ServiceDate,
        items = plan.Items.OrderBy(item => item.Position).Select(Describe),
    };

    private static object Describe(ServiceItem item) => new
    {
        id = item.Id,
        kind = item.Kind,
        label = item.Label,
        position = item.Position,
        @ref = item.Ref,
    };
}
