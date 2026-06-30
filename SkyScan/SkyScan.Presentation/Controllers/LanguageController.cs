using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using System;

namespace SkyScan.Presentation.Controllers
{
    public class LanguageController : Controller
    {
        [HttpPost]
        public IActionResult Toggle()
        {
            var currentCulture = Request.Cookies[CookieRequestCultureProvider.DefaultCookieName];
            string nextCulture = "en";

            if (string.IsNullOrEmpty(currentCulture) || currentCulture.Contains("en"))
            {
                nextCulture = "ar";
            }

            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(nextCulture)),
                new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) }
            );

            // Redirect back to the referrer page
            var referer = Request.Headers["Referer"].ToString();
            if (!string.IsNullOrEmpty(referer))
            {
                return Redirect(referer);
            }
            return RedirectToAction("Index", "Flight");
        }
    }
}
