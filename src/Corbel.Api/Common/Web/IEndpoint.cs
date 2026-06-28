namespace Corbel.Common.Web;

/// <summary>
/// Implemented by each vertical slice's endpoint. Auto-discovered at startup and mapped — so adding a
/// feature is "drop one file", with no central registration to edit.
/// </summary>
public interface IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app);
}
