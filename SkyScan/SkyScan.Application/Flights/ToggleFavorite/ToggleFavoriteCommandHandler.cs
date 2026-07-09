using MediatR;
using SkyScan.Core.Entities;
using SkyScan.Core.Repositories_Interfaces;

namespace SkyScan.Application.Flights.ToggleFavorite
{
    public class ToggleFavoriteCommandHandler : IRequestHandler<ToggleFavoriteCommand, ToggleFavoriteResult>
    {
        private readonly IFlightRepository _flightRepository;
        private readonly IPriceAlertRepository _priceAlertRepository;

        public ToggleFavoriteCommandHandler(IFlightRepository flightRepository, IPriceAlertRepository priceAlertRepository)
        {
            _flightRepository = flightRepository;
            _priceAlertRepository = priceAlertRepository;
        }

        public async Task<ToggleFavoriteResult> Handle(ToggleFavoriteCommand request, CancellationToken cancellationToken)
        {
            if (!DateTime.TryParse(request.DepartureTime, out var depTime))
            {
                return new ToggleFavoriteResult { Success = false, ErrorMessage = "Invalid date format" };
            }

            DateTime.TryParse(request.ArrivalTime, out var arrTime);

            var flight = await _flightRepository.EnsureFlightExistsAsync(
                request.FlightNumber,
                depTime,
                request.OriginIata,
                request.DestinationIata,
                request.AirlineName,
                arrTime == default ? depTime : arrTime,
                request.RedirectUrl ?? string.Empty
            );

            if (flight == null)
            {
                return new ToggleFavoriteResult { Success = false, ErrorMessage = "Could not resolve this flight's airports." };
            }

            // Note: EnsureTripExistsForFlightAsync is declared on IPriceAlertRepository despite being
            // flight-existence logic (Phase 0 Finding #18) — called as-is here; the filing issue is
            // out of scope for this phase (see Phase 2c report Flag F3).
            var trip = await _priceAlertRepository.EnsureTripExistsForFlightAsync(flight.FlightId, request.Price);

            var existing = await _priceAlertRepository.FindByUserAndTripAsync(request.UserId, trip.TripId);
            if (existing != null)
            {
                await _priceAlertRepository.DeleteAsync(existing);
                return new ToggleFavoriteResult { Success = true, Favorited = false };
            }

            await _priceAlertRepository.AddAsync(new PriceAlert
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                TripId = trip.TripId,
                TargetPrice = request.Price
            });

            return new ToggleFavoriteResult { Success = true, Favorited = true };
        }
    }
}
