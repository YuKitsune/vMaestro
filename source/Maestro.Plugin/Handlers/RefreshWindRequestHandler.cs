using Maestro.Contracts.Sessions;
using Maestro.Core.Configuration;
using MediatR;
using Serilog;
using vatsys;

namespace Maestro.Plugin.Handlers;

public record RefreshWindRequest(string AirportIdentifier)
    : IRequest;

/// <summary>
/// Updates the winds for the requested airport using the VATSIM METAR and GRIB winds.
/// </summary>
public class RefreshWindRequestHandler(IAirportConfigurationProvider airportConfigurationProvider, IMediator mediator, ILogger logger)
    : IRequestHandler<RefreshWindRequest>
{
    // TODO: Extract met lookups into separate services

    public async Task Handle(RefreshWindRequest request, CancellationToken cancellationToken)
    {
        logger.Verbose("Refreshing winds for {AirportIdentifier}", request.AirportIdentifier);

        await Task.CompletedTask;
        var airportConfiguration = airportConfigurationProvider.GetAirportConfiguration(request.AirportIdentifier);

        WindDto? surfaceWind;
        try
        {
            surfaceWind = await TryGetSurfaceWind(request.AirportIdentifier, cancellationToken);
            if (surfaceWind is null)
            {
                logger.Warning($"Could not find surface wind for {request.AirportIdentifier}");
                surfaceWind = new WindDto(0, 0);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to get surface wind for {AirportIdentifier}", request.AirportIdentifier);
            surfaceWind = new WindDto(0, 0);
        }

        WindDto? upperWind;
        try
        {
            upperWind = TryGetUpperWind(request.AirportIdentifier, airportConfiguration.UpperWindAltitude);
            if (upperWind is null)
            {
                logger.Warning($"Could not find upper wind for {request.AirportIdentifier}");
                upperWind = new WindDto(0, 0);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to get upper wind for {AirportIdentifier}", request.AirportIdentifier);
            upperWind = new WindDto(0, 0);
        }

        await mediator.Send(
            new ModifyWindRequest(
                request.AirportIdentifier,
                surfaceWind,
                upperWind,
                ManualWind: false),
            cancellationToken);
    }

    async Task<WindDto?> TryGetSurfaceWind(string airportIdentifier, CancellationToken cancellationToken)
    {
        var request = new MET.ProductRequest(MET.ProductType.VATSIM_METAR, airportIdentifier, subscribe: false);
        MET.Instance.RequestProduct(request);

        var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

        List<MET.Product>? responses;
        while (true)
        {
            responses = MET.Instance.GetProducts(request);
            if (responses is not null)
                break;

            await Task.Delay(TimeSpan.FromSeconds(1), linkedSource.Token);
        }

        var products = responses
            .OfType<MET.VATSIM_METAR>()
            .ToArray();

        if (!products.Any())
            return null;

        var latestMetar = products.First();
        var wind = MetarWindParser.Parse(latestMetar.Text);
        if (wind is null)
        {
            logger.Warning("Failed to parse METAR wind string: {METAR}", latestMetar.Text);
        }

        return wind;
    }

    WindDto? TryGetUpperWind(string airportIdentifier, int upperWindAltitude)
    {
        if (!GRIB.UseGRIB)
            return null;

        var airport = Airspace2.GetAirport(airportIdentifier);
        if (airport is null)
            return null;

        var airportLocation = airport.LatLong;
        var gribWind = GRIB.FindWind(upperWindAltitude, airportLocation);
        return new WindDto((int)gribWind.Direction, (int)gribWind.Speed);
    }
}
