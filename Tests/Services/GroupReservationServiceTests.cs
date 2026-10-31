using APIHandler.Constants;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Models.GroupReservation;
using APIHandler.Repositories.GroupReservations;
using APIHandler.Repositories.GuestSessions;
using APIHandler.Repositories.Orders;
using APIHandler.Repositories.Reservations;
using APIHandler.Repositories.Tables;
using APIHandler.Services.Auth;
using APIHandler.Services.GroupReservations;
using APIHandler.Services.GuestSessions;
using APIHandler.Services.Locations;
using AutoFixture.Xunit2;
using Moq;
using System.Text.Json;

namespace APIHandler.Tests.Services;

public class GroupReservationServiceTests
{
    private readonly Mock<IGroupReservationRepository> _groupReservationRepositoryMock = new();
    private readonly Mock<ITableRepository> _tableRepositoryMock = new();
    private readonly Mock<IReservationRepository> _reservationRepositoryMock = new();
    private readonly Mock<IGuestSessionRepository> _guestSessionRepositoryMock = new();
    private readonly Mock<IOrderRepository> _orderRepositoryMock = new();
    private readonly Mock<IGuestSessionService> _guestSessionServiceMock = new();
    private readonly Mock<IUserService> _userServiceMock = new();
    private readonly Mock<ILocationService> _locationServiceMock = new();

    private readonly GroupReservationService _service;

    public GroupReservationServiceTests()
    {
        _service = new GroupReservationService(
            _groupReservationRepositoryMock.Object,
            _tableRepositoryMock.Object,
            _reservationRepositoryMock.Object,
            _guestSessionRepositoryMock.Object,
            _orderRepositoryMock.Object,
            _guestSessionServiceMock.Object,
            _userServiceMock.Object,
            _locationServiceMock.Object);
    }

    [Fact]
    public async Task CreateGroupReservationAsync_WithOwner_CreatesOwnerGuestSession()
    {
        const string reservationId = "reservation-id";
        const string ownerUserId = "owner-user";

        GroupReservation group = await _service.CreateGroupReservationAsync(reservationId, ownerUserId);

        Assert.Equal(reservationId, group.ReservationId);
        _groupReservationRepositoryMock.Verify(r => r.SaveGroupReservationAsync(group), Times.Once);
        _guestSessionServiceMock.Verify(
            s => s.CreateGroupOwnerSessionAsync(ownerUserId, group.GroupReservationId),
            Times.Once);
    }

    [Fact]
    public async Task CreateGroupReservationIfDoesNotExistAsync_ExistingGroupWithoutOwnerSession_BackfillsOwnerSession()
    {
        const string ownerUserId = "owner-user";
        const string reservationId = "reservation-id";
        var group = new GroupReservation
        {
            GroupReservationId = "group-id",
            ReservationId = reservationId,
            ReservationToken = "token",
            Status = GroupReservationStatus.Open
        };
        _groupReservationRepositoryMock.Setup(r => r.GetByReservationIdAsync(reservationId))
            .ReturnsAsync(group);
        _guestSessionRepositoryMock.Setup(r => r.GetAllByGroupReservationIdAsync(group.GroupReservationId))
            .ReturnsAsync([]);

        await _service.CreateGroupReservationIfDoesNotExistAsync(ownerUserId, reservationId);

        _guestSessionServiceMock.Verify(
            s => s.CreateGroupOwnerSessionAsync(ownerUserId, group.GroupReservationId),
            Times.Once);
    }

    private Task SetupSubmitGroupReservationAsync(
        List<GuestSession> submittedSessions,
        Order? order = null)
    {
        var reservation = new Reservation
        {
            Id = "reservation-id",
            TableId = "table-id",
            CustomerId = "owner-user",
            WaiterId = "waiter-id"
        };
        var groupReservation = new GroupReservation
        {
            GroupReservationId = "group-id",
            ReservationId = reservation.Id,
            ReservationToken = "token",
            Status = GroupReservationStatus.Open
        };

        _groupReservationRepositoryMock.Setup(r => r.GetGroupReservationByIdAsync(groupReservation.GroupReservationId))
            .ReturnsAsync(groupReservation);
        _reservationRepositoryMock.Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);
        _guestSessionRepositoryMock.Setup(r => r.GetAllByGroupReservationIdAsync(groupReservation.GroupReservationId))
            .ReturnsAsync(submittedSessions);
        _orderRepositoryMock.Setup(r => r.GetOrderByReservationIdAsync(reservation.Id))
            .ReturnsAsync(order);

        return Task.CompletedTask;
    }

    [Fact]
    public async Task SubmitGroupReservationAsync_OwnerSubmits_UsesSubmittedSessionsAndConsolidatesDishes()
    {
        const string userId = "owner-user";
        const string reservationId = "reservation-id";
        const string groupReservationId = "group-id";
        var reservation = new Reservation
        {
            Id = reservationId,
            TableId = "table-id",
            CustomerId = userId,
            WaiterId = "waiter-id"
        };
        var groupReservation = new GroupReservation
        {
            GroupReservationId = groupReservationId,
            ReservationId = reservationId,
            ReservationToken = "token",
            Status = GroupReservationStatus.Open
        };
        var sessions = new List<GuestSession>
        {
            new()
            {
                Id = "submitted-1",
                GroupReservationId = groupReservationId,
                GuestName = "Guest 1",
                IsSubmitted = true,
                Dishes =
                [
                    new OrderItem { DishId = "dish-1", Quantity = 2 },
                    new OrderItem { DishId = "dish-2", Quantity = 1 }
                ]
            },
            new()
            {
                Id = "submitted-2",
                GroupReservationId = groupReservationId,
                GuestName = "Guest 2",
                IsSubmitted = true,
                Dishes = [new OrderItem { DishId = "dish-1", Quantity = 3 }]
            },
            new()
            {
                Id = "unsubmitted",
                GroupReservationId = groupReservationId,
                GuestName = "Guest 3",
                IsSubmitted = false,
                Dishes = [new OrderItem { DishId = "dish-ignored", Quantity = 100 }]
            }
        };
        var order = new Order
        {
            OrderId = "order-id",
            CustomerId = userId,
            ReservationId = reservationId,
            ReservationAddress = "address",
            ReservationTime = "time",
            Dishes = [new OrderItem { DishId = "dish-1", Quantity = 2 }],
            Status = OrderStatus.Created
        };

        _groupReservationRepositoryMock.Setup(r => r.GetGroupReservationByIdAsync(groupReservationId)).ReturnsAsync(groupReservation);
        _reservationRepositoryMock.Setup(r => r.GetReservationByIdAsync(reservationId)).ReturnsAsync(reservation);
        _guestSessionRepositoryMock.Setup(r => r.GetAllByGroupReservationIdAsync(groupReservationId)).ReturnsAsync(sessions);
        _orderRepositoryMock.Setup(r => r.GetOrderByReservationIdAsync(reservationId)).ReturnsAsync(order);

        var result = await _service.SubmitGroupReservationAsync(userId, groupReservationId, reservationId);

        Assert.Equal(2, result.Count);
        Assert.Equal(5, result.Single(dish => dish.DishId == "dish-1").Quantity);
        Assert.Equal(1, result.Single(dish => dish.DishId == "dish-2").Quantity);
        Assert.DoesNotContain(result, dish => dish.DishId == "dish-ignored");
        using JsonDocument responseJson = JsonDocument.Parse(JsonSerializer.Serialize(result));
        Assert.Equal(JsonValueKind.Array, responseJson.RootElement.ValueKind);
        Assert.Equal("dish-1", responseJson.RootElement[0].GetProperty("dishId").GetString());
        Assert.Equal(5, responseJson.RootElement[0].GetProperty("quantity").GetInt32());
        Assert.Equal(7, order.Dishes.Single(dish => dish.DishId == "dish-1").Quantity);
        Assert.DoesNotContain(order.Dishes, dish => dish.DishId == "dish-ignored");
        Assert.Equal(OrderStatus.SubmittedByCustomer, order.Status);
        Assert.Equal(GroupReservationStatus.Submitted, groupReservation.Status);
        _orderRepositoryMock.Verify(r => r.SaveOrderAsync(order), Times.Once);
        _groupReservationRepositoryMock.Verify(r => r.SaveGroupReservationAsync(groupReservation), Times.Once);
    }

    [Fact]
    public async Task SubmitGroupReservationAsync_NonOwner_ThrowsForbidden()
    {
        var reservation = new Reservation
        {
            Id = "reservation-id",
            TableId = "table-id",
            CustomerId = "owner-user",
            WaiterId = "waiter-id"
        };
        var groupReservation = new GroupReservation
        {
            GroupReservationId = "group-id",
            ReservationId = reservation.Id,
            ReservationToken = "token"
        };
        _groupReservationRepositoryMock.Setup(r => r.GetGroupReservationByIdAsync(groupReservation.GroupReservationId)).ReturnsAsync(groupReservation);
        _reservationRepositoryMock.Setup(r => r.GetReservationByIdAsync(reservation.Id)).ReturnsAsync(reservation);

        await Assert.ThrowsAsync<ActionForbiddenException>(() =>
            _service.SubmitGroupReservationAsync("other-user", groupReservation.GroupReservationId, reservation.Id));

        _guestSessionRepositoryMock.Verify(r => r.GetAllByGroupReservationIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SubmitGroupReservationAsync_GroupMissing_ThrowsNotFound()
    {
        _groupReservationRepositoryMock.Setup(r => r.GetGroupReservationByIdAsync("missing-group"))
            .ReturnsAsync((GroupReservation?)null);

        await Assert.ThrowsAsync<GroupReservationNotFoundException>(() =>
            _service.SubmitGroupReservationAsync("owner", "missing-group", "reservation-id"));
    }

    [Fact]
    public async Task SubmitGroupReservationAsync_ReservationMissing_ThrowsNotFound()
    {
        var groupReservation = new GroupReservation
        {
            GroupReservationId = "group-id",
            ReservationId = "missing-reservation",
            ReservationToken = "token"
        };
        _groupReservationRepositoryMock.Setup(r => r.GetGroupReservationByIdAsync(groupReservation.GroupReservationId)).ReturnsAsync(groupReservation);
        _reservationRepositoryMock.Setup(r => r.GetReservationByIdAsync(groupReservation.ReservationId))
            .ReturnsAsync((Reservation?)null);

        await Assert.ThrowsAsync<ReservationNotFoundException>(() =>
            _service.SubmitGroupReservationAsync("owner", groupReservation.GroupReservationId, groupReservation.ReservationId));
    }

    [Fact]
    public async Task SubmitGroupReservationAsync_NoSubmittedSessions_ThrowsValidation()
    {
        await SetupSubmitGroupReservationAsync(submittedSessions: []);

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SubmitGroupReservationAsync("owner-user", "group-id", "reservation-id"));
    }

    [Fact]
    public async Task SubmitGroupReservationAsync_SubmittedSessionsWithoutDishes_ThrowsValidation()
    {
        await SetupSubmitGroupReservationAsync(
        [
            new GuestSession
            {
                Id = "submitted",
                GroupReservationId = "group-id",
                GuestName = "Guest",
                IsSubmitted = true,
                Dishes = []
            }
        ]);

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SubmitGroupReservationAsync("owner-user", "group-id", "reservation-id"));
    }

    [Fact]
    public async Task SubmitGroupReservationAsync_OrderMissing_ThrowsValidation()
    {
        await SetupSubmitGroupReservationAsync(
        [
            new GuestSession
            {
                Id = "submitted",
                GroupReservationId = "group-id",
                GuestName = "Guest",
                IsSubmitted = true,
                Dishes = [new OrderItem { DishId = "dish-id", Quantity = 1 }]
            }
        ],
        order: null);

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SubmitGroupReservationAsync("owner-user", "group-id", "reservation-id"));
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationByTokenAsync_ValidInput_ReturnsGroupReservationInfo(
        GroupReservation groupReservation,
        Table table,
        Reservation reservation,
        Location location,
        string expectedUserName)
    {
        groupReservation.ReservationId = reservation.Id;
        groupReservation.Status = GroupReservationStatus.Open;
        reservation.TableId = table.Id;
        table.LocationId = location.Id;

        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByTokenAsync(groupReservation.ReservationToken))
            .ReturnsAsync(groupReservation);

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        _guestSessionRepositoryMock
            .Setup(r => r.CountByGroupReservationId(groupReservation.GroupReservationId))
            .ReturnsAsync(0);

        _tableRepositoryMock
            .Setup(r => r.GetTableAsync(table.Id))
            .ReturnsAsync(table);

        _locationServiceMock
            .Setup(s => s.GetLocationByIdAsync(table.LocationId))
            .ReturnsAsync(location);

        _userServiceMock
            .Setup(s => s.GetUserName(reservation.CustomerId!))
            .ReturnsAsync(expectedUserName);

        var result = await _service.GetGroupReservationByTokenAsync(groupReservation.ReservationToken);

        Assert.Equal(groupReservation.GroupReservationId, result.GroupReservationId);
        Assert.Equal(table.LocationId, result.LocationId);
        Assert.Equal(reservation.GuestCount, result.MaxGuestCount);
        Assert.Equal(reservation.StartTime, result.StartTime);
        Assert.Equal(reservation.EndTime, result.EndTime);
        Assert.Equal(table.TableNumber, result.TableNumber);
        Assert.Equal("Green & Tasty", result.RestaurantName);
        Assert.Equal(location.Address, result.LocationAddress);
        Assert.Equal(expectedUserName, result.InviterName);
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationByTokenAsync_InvalidToken_ThrowsNotFound(string token)
    {
        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByTokenAsync(token))
            .ReturnsAsync((GroupReservation?)null);

        await Assert.ThrowsAsync<GroupReservationNotFoundException>(() => _service.GetGroupReservationByTokenAsync(token));
    }

    [Theory]
    [InlineAutoData(GroupReservationStatus.Closed)]
    [InlineAutoData(GroupReservationStatus.Submitted)]
    public async Task GetGroupReservationByTokenAsync_GroupReservationIsNotOpen_ThrowsException(
        GroupReservationStatus status,
        GroupReservation groupReservation)
    {
        groupReservation.Status = status;

        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByTokenAsync(groupReservation.ReservationToken))
            .ReturnsAsync(groupReservation);

        await Assert.ThrowsAsync<GroupReservationExpiredException>(() => _service.GetGroupReservationByTokenAsync(groupReservation.ReservationToken));
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationByTokenAsync_WhenGuestCountIsMax_ThrowsGuestLimitExceeded(GroupReservation groupReservation, Reservation reservation)
    {
        groupReservation.Status = GroupReservationStatus.Open;
        groupReservation.ReservationId = reservation.Id;

        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByTokenAsync(groupReservation.ReservationToken))
            .ReturnsAsync(groupReservation);

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        _guestSessionRepositoryMock
            .Setup(r => r.CountByGroupReservationId(groupReservation.GroupReservationId))
            .ReturnsAsync(reservation.GuestCount);

        await Assert.ThrowsAsync<GuestLimitExceededException>(() => _service.GetGroupReservationByTokenAsync(groupReservation.ReservationToken));
    }

    [Theory]
    [AutoData]
    public async Task CloseGroupReservationAsync_ValidInput_UpdatesGroupReservationStatus(GroupReservation groupReservation)
    {
        _groupReservationRepositoryMock
            .Setup(r => r.GetByReservationIdAsync(groupReservation.ReservationId))
            .ReturnsAsync(groupReservation);

        GroupReservation? savedGroupReservation = null;
        _groupReservationRepositoryMock
            .Setup(r => r.SaveGroupReservationAsync(It.IsAny<GroupReservation>()))
            .Callback<GroupReservation>(r => savedGroupReservation = r);

        await _service.CloseGroupReservationAsync(groupReservation.ReservationId);

        Assert.NotNull(savedGroupReservation);
        Assert.Equal(GroupReservationStatus.Closed, savedGroupReservation.Status);
    }

    [Theory]
    [AutoData]
    public async Task CloseGroupReservationAsync_GroupReservationNotFound_DoesNotThrowException(string reservationId)
    {
        _groupReservationRepositoryMock
            .Setup(r => r.GetByReservationIdAsync(reservationId))
            .ReturnsAsync((GroupReservation?)null);

        await _service.CloseGroupReservationAsync(reservationId);
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationInviteTokenAsync_ValidInput_ReturnsToken(Reservation reservation, GroupReservation groupReservation)
    {
        reservation.Status = ReservationStatus.Reserved;
        reservation.GuestCount = 3;
        groupReservation.ReservationId = reservation.Id;
        groupReservation.Status = GroupReservationStatus.Open;

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        _groupReservationRepositoryMock
            .Setup(r => r.GetByReservationIdAsync(reservation.Id))
            .ReturnsAsync(groupReservation);

        GetGroupInviteTokenResponse result = await _service.GetGroupReservationInviteTokenAsync(reservation.Id);

        Assert.Equal(groupReservation.ReservationToken, result.Token);
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationInviteTokenAsync_InvalidReservationId_ThrowsNotFound(string reservationId)
    {
        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservationId))
            .ReturnsAsync((Reservation?)null);

        await Assert.ThrowsAsync<ReservationNotFoundException>(() => _service.GetGroupReservationInviteTokenAsync(reservationId));
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationInviteTokenAsync_OneGuest_ThrowsValidationException(Reservation reservation)
    {
        reservation.Status = ReservationStatus.Reserved;
        reservation.GuestCount = 1;

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        await Assert.ThrowsAsync<ValidationException>(() => _service.GetGroupReservationInviteTokenAsync(reservation.Id));
    }

    [Theory]
    [InlineAutoData(ReservationStatus.Completed)]
    [InlineAutoData(ReservationStatus.Cancelled)]
    [InlineAutoData(ReservationStatus.InProgress)]
    public async Task GetGroupReservationInviteTokenAsync_StatusIsNotReserved_ThrowsValidationException(ReservationStatus reservationStatus, Reservation reservation)
    {
        reservation.Status = reservationStatus;
        reservation.GuestCount = 1;

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        await Assert.ThrowsAsync<ValidationException>(() => _service.GetGroupReservationInviteTokenAsync(reservation.Id));
    }

    [Theory]
    [InlineAutoData(GroupReservationStatus.Closed, typeof(GroupReservationExpiredException))]
    [InlineAutoData(GroupReservationStatus.Submitted, typeof(GroupReservationSubmittedException))]
    public async Task GetGroupReservationInviteTokenAsync_GroupStatusIsNotOpen_ThrowsException(GroupReservationStatus status, Type type, Reservation reservation, GroupReservation groupReservation)
    {
        reservation.Status = ReservationStatus.Reserved;
        reservation.GuestCount = 3;
        groupReservation.ReservationId = reservation.Id;
        groupReservation.Status = status;

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        _groupReservationRepositoryMock
            .Setup(r => r.GetByReservationIdAsync(reservation.Id))
            .ReturnsAsync(groupReservation);

        await Assert.ThrowsAsync(type, () => _service.GetGroupReservationInviteTokenAsync(reservation.Id));
    }
}
