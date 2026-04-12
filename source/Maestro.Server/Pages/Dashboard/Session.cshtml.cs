using Maestro.Contracts.Flights;
using Maestro.Contracts.Sessions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Maestro.Server.Pages.Dashboard;

public class SessionModel : PageModel
{
    private readonly SessionCache _sessionCache;
    private readonly IConnectionManager _connectionManager;

    public SessionModel(SessionCache sessionCache, IConnectionManager connectionManager)
    {
        _sessionCache = sessionCache;
        _connectionManager = connectionManager;
    }

    [BindProperty(SupportsGet = true)]
    public string Environment { get; set; } = "";

    [BindProperty(SupportsGet = true)]
    public string Airport { get; set; } = "";

    public SessionDto? Session { get; private set; }
    public Connection[] Connections { get; private set; } = [];
    public FlightDto[] Flights { get; private set; } = [];
    public int TotalFlights { get; private set; }

    public IActionResult OnGet()
    {
        Session = _sessionCache.Get(Environment, Airport);
        if (Session is null)
            return NotFound();

        Connections = _connectionManager.GetConnections(Environment, Airport);

        Flights = Session.Sequence.Flights
            .OrderBy(f => f.LandingTime)
            .ToArray();

        TotalFlights = Flights.Length;

        return Page();
    }
}
