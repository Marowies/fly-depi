using MediatR;
using Microsoft.AspNetCore.Mvc;
using SkyScan.Application.Languages.SetLanguage;

namespace SkyScan.Presentation.Controllers
{
    public class LanguageController : Controller
    {
        private readonly IMediator _mediator;

        public LanguageController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        public async Task<IActionResult> Set(string culture, string returnUrl)
        {
            await _mediator.Send(new SetLanguageCommand(culture));

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("Index", "Flight");
        }
    }
}
