using MediatR;
using Microsoft.AspNetCore.Mvc;
using SkyScan.Application.Currency.SetCurrency;

namespace SkyScan.Presentation.Controllers
{
    public class CurrencyController : Controller
    {
        private readonly IMediator _mediator;

        public CurrencyController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        public async Task<IActionResult> Set(string code)
        {
            await _mediator.Send(new SetCurrencyCommand(code));

            var referer = Request.Headers["Referer"].ToString();
            if (!string.IsNullOrEmpty(referer))
            {
                return Redirect(referer);
            }
            return RedirectToAction("Index", "Flight");
        }
    }
}
