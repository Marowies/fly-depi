using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;

namespace SkyScan.Presentation.Controllers
{
    public class CurrencyController : Controller
    {
        [HttpGet]
        public IActionResult Set(string code)
        {
            if (!string.IsNullOrEmpty(code))
            {
                Response.Cookies.Append(
                    "SelectedCurrency",
                    code.ToUpper(),
                    new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) }
                );
            }

            var referer = Request.Headers["Referer"].ToString();
            if (!string.IsNullOrEmpty(referer))
            {
                return Redirect(referer);
            }
            return RedirectToAction("Index", "Flight");
        }
    }
}
