using SkyScan.Application.DTOs;
using SkyScan.Application.Validators;
using SkyScan.Core.Constants;
using Xunit;
using FluentValidation.TestHelper;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SkyScan.Tests.Validators
{
    public class FlightSearchRequestValidatorTests
    {
        private readonly FlightSearchRequestValidator _validator;

        public FlightSearchRequestValidatorTests()
        {
            _validator = new FlightSearchRequestValidator();
        }

        [Fact]
        public void Should_Have_Error_When_Legs_Are_Empty()
        {
            var model = new FlightSearchRequestDto { Legs = new List<FlightLegDto>() };
            var result = _validator.TestValidate(model);
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.PropertyName == "Legs");
        }

        [Fact]
        public void Should_Have_Error_When_Origin_And_Destination_Are_Same()
        {
            var cityId = Guid.NewGuid();
            var model = new FlightSearchRequestDto
            {
                Legs = new List<FlightLegDto>
                {
                    new FlightLegDto
                    {
                        OriginCityId = cityId,
                        DestinationCityId = cityId,
                        DepartureDate = DateTime.Today.AddDays(1)
                    }
                }
            };
            var result = _validator.TestValidate(model);
            Assert.False(result.IsValid);
            // The validator uses RuleFor(x => x).Must(...) on the leg, which might have different property names depending on how RuleForEach is called.
            // In the failure log it showed "Legs[0]"
            Assert.True(result.Errors.Any(e => e.PropertyName.Contains("Legs[0]")));
        }

        [Fact]
        public void Should_Have_Error_When_DepartureDate_Is_In_Past()
        {
            var model = new FlightSearchRequestDto
            {
                Legs = new List<FlightLegDto>
                {
                    new FlightLegDto
                    {
                        OriginCityId = Guid.NewGuid(),
                        DestinationCityId = Guid.NewGuid(),
                        DepartureDate = DateTime.Today.AddDays(-1)
                    }
                }
            };
            var result = _validator.TestValidate(model);
            Assert.False(result.IsValid);
            Assert.True(result.Errors.Any(e => e.PropertyName.Contains("DepartureDate")));
        }

        [Fact]
        public void Should_Have_Error_When_ReturnDate_Is_Before_DepartureDate_In_RoundTrip()
        {
            var departureDate = DateTime.Today.AddDays(5);
            var returnDate = DateTime.Today.AddDays(3);
            var model = new FlightSearchRequestDto
            {
                TripType = TripType.RoundTrip,
                ReturnDate = returnDate,
                Legs = new List<FlightLegDto>
                {
                    new FlightLegDto
                    {
                        OriginCityId = Guid.NewGuid(),
                        DestinationCityId = Guid.NewGuid(),
                        DepartureDate = departureDate
                    }
                }
            };
            var result = _validator.TestValidate(model);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void Should_Not_Have_Error_When_Request_Is_Valid()
        {
            var model = new FlightSearchRequestDto
            {
                TripType = TripType.OneWay,
                Legs = new List<FlightLegDto>
                {
                    new FlightLegDto
                    {
                        OriginCityId = Guid.NewGuid(),
                        DestinationCityId = Guid.NewGuid(),
                        DepartureDate = DateTime.Today.AddDays(1)
                    }
                }
            };
            var result = _validator.TestValidate(model);
            Assert.True(result.IsValid);
        }
    }
}
