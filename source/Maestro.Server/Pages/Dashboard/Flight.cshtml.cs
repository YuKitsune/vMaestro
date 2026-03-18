using Maestro.Core.Messages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Maestro.Server.Pages;

public class FlightModel : PageModel
{
    private readonly SessionCache _sessionCache;

    public FlightModel(SessionCache sessionCache)
    {
        _sessionCache = sessionCache;
    }

    [BindProperty(SupportsGet = true)]
    public string Partition { get; set; } = "";

    [BindProperty(SupportsGet = true)]
    public string Airport { get; set; } = "";

    [BindProperty(SupportsGet = true)]
    public string Callsign { get; set; } = "";

    public FlightMessage? Flight { get; private set; }

    public IActionResult OnGet()
    {
        var session = _sessionCache.Get(Partition, Airport);
        if (session is null)
            return NotFound();

        Flight = session.Sequence.Flights.FirstOrDefault(f => f.Callsign == Callsign)
            ?? session.PendingFlights.FirstOrDefault(f => f.Callsign == Callsign)
            ?? session.DeSequencedFlights.FirstOrDefault(f => f.Callsign == Callsign);

        if (Flight is null)
            return NotFound();

        return Page();
    }
}
