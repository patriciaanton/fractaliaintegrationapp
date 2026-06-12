using FractaliaIntegrationApp.Services;
using Microsoft.AspNetCore.Mvc;

namespace FractaliaIntegrationApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PartnersController : ControllerBase
{
    private readonly IFractaliaClient _fractaliaClient;

    public PartnersController(IFractaliaClient fractaliaClient)
    {
        _fractaliaClient = fractaliaClient;
    }

    [HttpGet]
    public async Task<IActionResult> GetPartners(CancellationToken cancellationToken)
    {
        var partners = await _fractaliaClient.GetPartnersAsync(cancellationToken);

        if (partners is null)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                message = "Error en la API externa: no se pudo obtener la lista de partners."
            });
        }

        return Ok(partners);
    }
}
