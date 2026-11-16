using Microsoft.AspNetCore.Mvc;

namespace ShortLink.API.Controllers;

/// <summary>
/// Base API controller providing common routing attributes and helper methods.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
    protected string GetBaseUrl()
    {
        return $"{Request.Scheme}://{Request.Host}";
    }
}
