using __ProjectName__.Models;
using __ProjectName__.Services;

namespace __ProjectName__.Endpoints;

/// <summary>O CRUD de <see cref="Item"/> (RF-13).</summary>
public static class ItemEndpoints
{
    /// <summary>
    /// Mapeia <c>GET /items</c>, <c>GET /items/{id}</c>, <c>POST /items</c>,
    /// <c>PUT /items/{id}</c> e <c>DELETE /items/{id}</c>.
    /// </summary>
    /// <remarks>
    /// Não há regra de proprietário: os itens são compartilhados por quem tiver acesso à API
    /// (RF-15).
    /// </remarks>
    public static IEndpointRouteBuilder MapItems(this IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        RouteGroupBuilder items = routes.MapGroup("/items");
__ItemsAuthorization__

        items.MapGet("", async (ItemService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(cancellationToken)))
            .WithName("ListItems")
            .Produces<List<Item>>(StatusCodes.Status200OK);

        items.MapGet("/{id:int}", async (
            int id,
            ItemService service,
            CancellationToken cancellationToken) =>
            await service.FindAsync(id, cancellationToken) is Item item
                ? Results.Ok(item)
                : Results.NotFound())
            .WithName("GetItem")
            .Produces<Item>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        items.MapPost("", async (
            ItemInput input,
            ItemService service,
            CancellationToken cancellationToken) =>
        {
            if (ItemService.Validate(input) is IDictionary<string, string[]> errors)
            {
                return Results.ValidationProblem(errors);
            }

            Item created = await service.CreateAsync(input, cancellationToken);

            return Results.Created($"/items/{created.Id}", created);
        })
            .WithName("CreateItem")
            .Produces<Item>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        items.MapPut("/{id:int}", async (
            int id,
            ItemInput input,
            ItemService service,
            CancellationToken cancellationToken) =>
        {
            if (ItemService.Validate(input) is IDictionary<string, string[]> errors)
            {
                return Results.ValidationProblem(errors);
            }

            return await service.ReplaceAsync(id, input, cancellationToken)
                ? Results.NoContent()
                : Results.NotFound();
        })
            .WithName("ReplaceItem")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound);

        items.MapDelete("/{id:int}", async (
            int id,
            ItemService service,
            CancellationToken cancellationToken) =>
            await service.DeleteAsync(id, cancellationToken)
                ? Results.NoContent()
                : Results.NotFound())
            .WithName("DeleteItem")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        return routes;
    }
}
