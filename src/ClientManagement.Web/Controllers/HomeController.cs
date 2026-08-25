using System.Net.Http.Headers;
using System.Text.Json;
using ClientManagement.Domain;
using Microsoft.AspNetCore.Mvc;

namespace ClientManagement.Web.Controllers;

public sealed class HomeController(IHttpClientFactory clients) : Controller
{
    private readonly JsonSerializerOptions json = new(JsonSerializerDefaults.Web);

    public async Task<IActionResult> Index(string? search, int page = 1)
    {
        var response = await Send(HttpMethod.Get, $"api/clients?search={Uri.EscapeDataString(search ?? string.Empty)}&page={page}&pageSize=25");
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized) return RedirectToAction("Login", "Account", new { returnUrl = Request.Path + Request.QueryString });
        if (!response.IsSuccessStatusCode) return Problem("The client service is unavailable.");
        return View(new HomeViewModel { Search = search ?? string.Empty, Page = JsonSerializer.Deserialize<ClientPage>(await response.Content.ReadAsStringAsync(), json) ?? new() });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int? id) { if (id is null) return View(new Client()); var response = await Send(HttpMethod.Get, $"api/clients/{id}"); if (!response.IsSuccessStatusCode) return NotFound(); return View(JsonSerializer.Deserialize<Client>(await response.Content.ReadAsStringAsync(), json)); }

    [HttpPost]
    public async Task<IActionResult> Edit(Client client) { var response = await Send(HttpMethod.Post, "api/clients", client); if (!response.IsSuccessStatusCode) { ModelState.AddModelError(string.Empty, await response.Content.ReadAsStringAsync()); return View(client); } return RedirectToAction(nameof(Index)); }

    [HttpPost]
    public async Task<IActionResult> Delete(int id) { await Send(HttpMethod.Delete, $"api/clients/{id}"); return RedirectToAction(nameof(Index)); }

    public async Task<IActionResult> Export() { var response = await Send(HttpMethod.Get, "api/clients/export"); if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized) return RedirectToAction("Login", "Account"); return File(await response.Content.ReadAsByteArrayAsync(), "text/csv", "clients.csv"); }

    private async Task<HttpResponseMessage> Send(HttpMethod method, string path, object? body = null)
    {
        var client = clients.CreateClient("ClientService"); using var request = new HttpRequestMessage(method, path); var auth = HttpContext.Session.GetString("BasicAuth"); if (!string.IsNullOrEmpty(auth)) request.Headers.Authorization = new AuthenticationHeaderValue("Basic", auth); if (body is not null) request.Content = JsonContent.Create(body); return await client.SendAsync(request);
    }
}

public sealed class HomeViewModel { public string Search { get; set; } = string.Empty; public ClientPage Page { get; set; } = new(); }