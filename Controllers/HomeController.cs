using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MyKicksBuddy.Models;
using MyKicksBuddy.Models.Dtos;
using MyKicksBuddy.Services;

namespace MyKicksBuddy.Controllers;

public class HomeController : Controller
{
    private readonly IOrderService _orderService;
    private readonly ILogger<HomeController> _logger;

    public HomeController(IOrderService orderService, ILogger<HomeController> logger)
    {
        _orderService = orderService;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            return View(await _orderService.GetAllServicesAsync());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Landing page services could not be loaded.");
            return View((IReadOnlyList<ServiceOptionDto>)[]);
        }
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
