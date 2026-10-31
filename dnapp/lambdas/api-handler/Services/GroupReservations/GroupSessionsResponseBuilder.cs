
using System;
using System.Collections.Generic;
using System.Linq;
using APIHandler.Entities;
using APIHandler.Models.GroupReservation;

namespace APIHandler.Services.GroupReservations;

public static class GroupSessionsResponseBuilder
{
    public static GetGroupSessionsResponse Build(
        IEnumerable<GuestSession> sessions,
        IEnumerable<Dish> dishes)
    {
        var dishesById = dishes.ToDictionary(d => d.Id);

        var response = new GetGroupSessionsResponse();

        foreach (var session in sessions)
        {
            var participant = new GroupSessionParticipantResponse
            {
                Id = session.Id,
                Type = session.IsOwnerSession ? "Owner" : "Guest",
                Name = session.GuestName,
                IsSubmitted = session.IsSubmitted
            };

            foreach (var orderItem in session.Dishes)
            {
                if (!dishesById.TryGetValue(
                    orderItem.DishId, out var dish))
                {
                    throw new InvalidOperationException(
                        $"Dish {orderItem.DishId} was not found.");
                }

                participant.Items.Add(new GroupSessionItemResponse
                {
                    Dish = new GroupSessionDishResponse
                    {
                        Id = dish.Id,
                        Name = dish.Name,
                        Price = dish.Price
                    },
                    Quantity = orderItem.Quantity
                });

                participant.Total += dish.Price * orderItem.Quantity;
            }

            response.Participants.Add(participant);
            response.Total += participant.Total;
        }

        return response;
    }
}
