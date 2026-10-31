
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace APIHandler.Models.GroupReservation;

public class GetGroupSessionsResponse
{
    [JsonPropertyName("participants")]
    public List<GroupSessionParticipantResponse> Participants { get; set; } = new();

    [JsonPropertyName("total")]
    public decimal Total { get; set; }
}

public class GroupSessionParticipantResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("items")]
    public List<GroupSessionItemResponse> Items { get; set; } = new();

    [JsonPropertyName("total")]
    public decimal Total { get; set; }

    [JsonPropertyName("isSubmitted")]
    public bool IsSubmitted { get; set; }
}

public class GroupSessionItemResponse
{
    [JsonPropertyName("dish")]
    public GroupSessionDishResponse Dish { get; set; } = new();

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }
}

public class GroupSessionDishResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("price")]
    public decimal Price { get; set; }
}
