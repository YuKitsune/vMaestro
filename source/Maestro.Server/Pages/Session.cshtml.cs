using Maestro.Core.Messages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Maestro.Server.Pages;

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
    public string Partition { get; set; } = "";

    [BindProperty(SupportsGet = true)]
    public string Airport { get; set; } = "";

    [BindProperty(SupportsGet = true)]
    public int Page { get; set; } = 1;

    public SessionMessage? Session { get; private set; }
    public Connection[] Connections { get; private set; } = [];
    public FlightMessage[] PagedFlights { get; private set; } = [];
    public int TotalPages { get; private set; }
    public int TotalFlights { get; private set; }

    public const int PageSize = 10;

    public IActionResult OnGet()
    {
        Session = _sessionCache.Get(Partition, Airport);
        if (Session is null)
            return NotFound();

        Connections = _connectionManager.GetConnections(Partition, Airport);

        var allFlights = Session.Sequence.Flights
            .OrderBy(f => f.LandingTime)
            .ToArray();

        TotalFlights = allFlights.Length;
        TotalPages = (int)Math.Ceiling(TotalFlights / (double)PageSize);
        Page = Math.Clamp(Page, 1, Math.Max(1, TotalPages));

        PagedFlights = allFlights
            .Skip((Page - 1) * PageSize)
            .Take(PageSize)
            .ToArray();

        return Page();
    }
}
