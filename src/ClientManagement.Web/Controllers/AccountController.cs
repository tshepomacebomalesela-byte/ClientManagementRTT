using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace ClientManagement.Web.Controllers;

public sealed class AccountController : Controller
{
    [HttpGet]
    public IActionResult Login(string? returnUrl = null) => View(new LoginModel { ReturnUrl = returnUrl ?? "/" });

    [HttpPost]
    public IActionResult Login(LoginModel model)
    {
        if (!ModelState.IsValid) return View(model);
        HttpContext.Session.SetString("BasicAuth", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{model.Username}:{model.Password}")));
        return LocalRedirect(model.ReturnUrl ?? "/");
    }

    public IActionResult Logout() { HttpContext.Session.Clear(); return RedirectToAction(nameof(Login)); }
}

public sealed class LoginModel
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ReturnUrl { get; set; } = "/";
}