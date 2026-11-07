using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Entities;
using APIHandler.Helpers;
using APIHandler.Models.GuestReservation;
using APIHandler.Repositories.GroupReservations;
using APIHandler.Repositories.Reservations;
using APIHandler.Repositories.Tables;
using APIHandler.Services.Auth;
using APIHandler.Services.Locations;
using System;
using System.Threading.Tasks;

namespace APIHandler.Handlers.GroupReservations;

// Read-only Owner view. Unlike the invite-token endpoint, this supports submitted groups.
public sealed class GetOwnerGroupReservationHandler(
    IReservationRepository reservations,
    IGroupReservationRepository groups,
    ITableRepository tables,
    ILocationService locations,
    IUserService users) : IApiRequestHandler
{
    public async Task<APIGatewayProxyResponse> HandleRequest(APIGatewayProxyRequest request, ILambdaContext context)
    {
        string? userId = UserContextHelper.GetAuthenticatedUserId(request);
        if (string.IsNullOrWhiteSpace(userId))
            return ResponseBuilder.CreateErrorResponse(401, "Authentication required.", context.Logger);

        if (request.PathParameters is null ||
            !request.PathParameters.TryGetValue("reservationId", out string? reservationId) ||
            string.IsNullOrWhiteSpace(reservationId))
            return ResponseBuilder.CreateErrorResponse(400, "Reservation ID is required.", context.Logger);

        Reservation? reservation = await reservations.GetReservationByIdAsync(reservationId);
        if (reservation is null)
            return ResponseBuilder.CreateErrorResponse(404, "Reservation not found.", context.Logger);

        if (!string.Equals(reservation.CustomerId, userId, StringComparison.OrdinalIgnoreCase))
            return ResponseBuilder.CreateErrorResponse(403, "Only the reservation owner can view this group.", context.Logger);

        GroupReservation? group = await groups.GetByReservationIdAsync(reservationId);
        if (group is null)
            return ResponseBuilder.CreateErrorResponse(404, "Group reservation not found.", context.Logger);

        Table? table = await tables.GetTableAsync(reservation.TableId);
        if (table is null)
            return ResponseBuilder.CreateErrorResponse(404, "Table not found.", context.Logger);

        var location = await locations.GetLocationByIdAsync(table.LocationId);
        string ownerName = await users.GetUserName(userId) ?? "Unknown";

        var result = new GetGroupReservationResponse(
            group.GroupReservationId,
            table.LocationId,
            "Green & Tasty",
            location?.Address ?? "Unknown Address",
            reservation.StartTime,
            reservation.EndTime,
            reservation.GuestCount,
            table.TableNumber,
            ownerName);
        return ResponseBuilder.CreateSuccessResponse(result);
    }
}
