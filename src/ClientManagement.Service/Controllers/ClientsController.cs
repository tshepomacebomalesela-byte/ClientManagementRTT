using ClientManagement.Data;
using ClientManagement.Domain;
using Microsoft.AspNetCore.Mvc;

namespace ClientManagement.Service.Controllers;

[ApiController]
[Route("api/clients")]
public sealed class ClientsController : ControllerBase
{
    private readonly IClientRepository repository;

    public ClientsController(IClientRepository repository) { this.repository = repository; }

    [HttpGet]
    public ActionResult<ClientPage> Search([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 25) => repository.Search(search, page, pageSize);

    [HttpGet("{id:int}")]
    public ActionResult<Client> Get(int id) => repository.Get(id) is { } client ? client : NotFound();

    [HttpPost]
    public IActionResult Save([FromBody] Client client) { if (string.IsNullOrWhiteSpace(client.FirstName) || string.IsNullOrWhiteSpace(client.LastName) || string.IsNullOrWhiteSpace(client.Gender)) return BadRequest("First name, last name and gender are required."); return Ok(new { id = repository.Save(client) }); }

    [HttpDelete("{id:int}")]
    public IActionResult Delete(int id) { repository.Delete(id); return NoContent(); }

    [HttpGet("export")]
    public IActionResult Export() => File(repository.ExportCsv(), "text/csv", $"clients-{DateTime.UtcNow:yyyyMMdd-HHmm}.csv");
}