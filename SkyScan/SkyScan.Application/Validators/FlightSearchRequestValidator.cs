using FluentValidation;
using SkyScan.Application.DTOs;
using SkyScan.Core.Constants;
using System;

namespace SkyScan.Application.Validators
{
    public class FlightSearchRequestValidator : AbstractValidator<FlightSearchRequestDto>
    {
        public FlightSearchRequestValidator()
        {
            RuleFor(x => x.Legs)
                .NotEmpty().WithMessage("At least one flight leg is required.");

            RuleForEach(x => x.Legs).SetValidator(new FlightLegValidator());

            // Round-trip specific validation
            RuleFor(x => x.ReturnDate)
                .NotEmpty()
                .When(x => x.TripType == TripType.RoundTrip)
                .WithMessage("Return date is required for round-trip flights.");

            RuleFor(x => x)
                .Must(x => x.ReturnDate > x.Legs[0].DepartureDate)
                .When(x => x.TripType == TripType.RoundTrip && x.ReturnDate.HasValue && x.Legs.Count > 0)
                .WithMessage("Return date must be after the departure date.");
        }
    }

    public class FlightLegValidator : AbstractValidator<FlightLegDto>
    {
        public FlightLegValidator()
        {
            RuleFor(x => x.OriginCityId)
                .NotEmpty().WithMessage("Origin city is required.");

            RuleFor(x => x.DestinationCityId)
                .NotEmpty().WithMessage("Destination city is required.");

            RuleFor(x => x.DepartureDate)
                .GreaterThanOrEqualTo(DateTime.Today)
                .WithMessage("Departure date cannot be in the past.");

            RuleFor(x => x)
                .Must(x => x.OriginCityId != x.DestinationCityId)
                .WithMessage("Origin and destination cities cannot be the same.");
        }
    }
}
