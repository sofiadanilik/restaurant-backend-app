using APIHandler.Models.Orders;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace APIHandler.Models.GuestSessions;

public record GetAllSessionsResponse(
    [property: JsonPropertyName("participants")] IEnumerable<Participant> Participants,
    [property: JsonPropertyName("total")] decimal Total);

public record Participant(
    [property: JsonPropertyName("id")]
    string? Id,
    [property: JsonPropertyName("type")]
    [property: JsonConverter(typeof(JsonStringEnumConverter<ParticipantType>))] ParticipantType Type,
    [property: JsonPropertyName("name")]
    string Name,
    [property: JsonPropertyName("items")]
    IEnumerable<ParticipantDishResponse> Items,
    [property: JsonPropertyName("total")]
    decimal Total,
    [property: JsonPropertyName("isSubmitted")]
    bool IsSubmitted);

public record ParticipantDishResponse(
    [property: JsonPropertyName("dish")] ParticipantDish Dish,
    [property: JsonPropertyName("quantity")] int Quantity);

public record ParticipantDish(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("price")] decimal Price);

public enum ParticipantType
{
    Owner,
    Guest
}
