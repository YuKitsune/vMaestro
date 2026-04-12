using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Maestro.Server.Pages;

public class IndexModel : PageModel
{
    private readonly SessionCache _sessionCache;

    public IndexModel(SessionCache sessionCache)
    {
        _sessionCache = sessionCache;
    }

    public IEnumerable<SessionKey> Sessions { get; private set; } = [];

    public void OnGet()
    {
        Sessions = _sessionCache.GetAll().Select(s => s.Key);
    }
}
