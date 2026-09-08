using Microsoft.AspNetCore.Mvc;

namespace LoadBalancer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SearchController : ControllerBase
{
    private static readonly string[] Backends =
    {
        "http://localhost:5223",
        "http://localhost:5224"
    };

    private static int _counter = -1;

    [HttpGet("instances")]
    public void GetInstances()
    {
        var backend = Backends[Interlocked.Increment(ref _counter) % Backends.Length];
        Redirect($"{backend}/api/search/instances{Request.QueryString}");
    }
}
