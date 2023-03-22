using API.Entities;
using API.Interface;
using BeautyContestAPI.Entities;
using BeautyContestAPI.Interface;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc;
using Stripe;
using Stripe.Checkout;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentController : BaseApiController
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<PaymentController> _logger;
    private readonly IUserRepository _userRepository;
    private readonly IAuditRepository _auditRepository;
    private static string s_wasmClientURL = string.Empty;

    public PaymentController(
        IConfiguration configuration,
        ILogger<PaymentController> logger,
        IUserRepository userRepository,
        IAuditRepository auditRepository
        )
    {
        _configuration = configuration;
        _logger = logger;
        _userRepository = userRepository;
        _auditRepository = auditRepository;
    }

    [HttpPost]
    public async Task<ActionResult> CheckoutOrder([FromBody] Vote product, [FromServices] IServiceProvider sp)
    {
        var referer = Request.Headers.Referer;
        s_wasmClientURL = referer[0];

        var server = sp.GetRequiredService<IServer>();

        var serverAddressesFeature = server.Features.Get<IServerAddressesFeature>();

        string thisApiUrl = null;

        if (serverAddressesFeature is not null)
        {
            thisApiUrl = serverAddressesFeature.Addresses.FirstOrDefault();
        }

        if (thisApiUrl is not null)
        {
            var sessionId = await CheckOut(product, thisApiUrl);
            var pubKey = _configuration["Stripe:PubKey"];

            var checkoutOrderResponse = new CheckoutOrderResponse()
            {
                SessionId = sessionId,
                PublishKey = pubKey
            };

            return Ok(checkoutOrderResponse);
        }
        else
        {
            return StatusCode(500);
        }
    }

    [NonAction]
    public async Task<string> CheckOut(Vote product, string thisApiUrl)
    {
        Stripe.StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];

        string description = "You have voted " + product.Price / 100 + " to " + product.Description;

        var options = new SessionCreateOptions
        {
            SuccessUrl = $"{thisApiUrl}/api/payment/success?sessionId=" + "{CHECKOUT_SESSION_ID}" + $"&name={product.Title}",
            CancelUrl = s_wasmClientURL,
            PaymentMethodTypes = new List<string>
            {
                "card",
                "fpx",
                //"alipay",
                "grabpay",
                //"apple_pay"
            },
            LineItems = new List<SessionLineItemOptions>
            {
                new()
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        UnitAmount = product.Price,
                        Currency = "MYR",
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = product.Description,
                            Description = description
                        },
                    },
                    Quantity = 1,
                },
            },
            PaymentIntentData = new SessionPaymentIntentDataOptions
            {
                Metadata = new Dictionary<string, string>()
                {
                    {"Username", product.Title},
                    {"Description", description},
                    {"Url", s_wasmClientURL}
                }
            },
            Mode = "payment"
        };

        var service = new SessionService();
        var session = await service.CreateAsync(options);

        return session.Id;
    }

    [HttpGet("success")]
    public async Task<ActionResult> CheckoutSuccess(string sessionId, string name)
    {
        // var result = await _auditRepository.GetAuditAsync(sessionId);

        // if (result != null) { return Redirect(s_wasmClientURL); }

        // Audit audit = new Audit()
        // {
        //     SessionId = sessionId,
        // };
        // _auditRepository.AddAuditLog(audit);
        // await _auditRepository.SaveAllAsync();

        // var sessionService = new SessionService();
        // var session = sessionService.Get(sessionId);
        // int total = Convert.ToInt32(session.AmountTotal / 100);

        // AppUser user = await _userRepository.GetUserByUsernameAsync(name);

        // user.Vote += total;

        // _userRepository.Update(user);

        // await _userRepository.SaveAllAsync();

        // string msg = $"You have voted {total} to {name}";

        // _logger.LogCritical("\n PaymentIntentId : {0} \n Payment Method : {1} \n Description: {2}",
        // session.PaymentIntentId,
        // session.PaymentMethodTypes,
        // msg);

        return Redirect(s_wasmClientURL);
    }

    [HttpPost("webhook")]
    public async Task<ActionResult> WebhookHandler() 
    {
        Stripe.StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];

        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
        _logger.LogCritical(json);

        if(!json.Contains("charge.succeeded")) 
        {
            return Ok();
        }        
        try
        {
            var stripeEvent = EventUtility.ConstructEvent(json,
                Request.Headers["Stripe-Signature"], _configuration["Stripe:WebHookKey"]);
            _logger.LogCritical(json);

            // Handle the event
            if(stripeEvent.Type == Events.ChargeSucceeded)
            {
                var session = stripeEvent.Data.Object as Charge;
                _logger.LogCritical("before");

                if (session.Status == "succeeded")
                {
                    string name = session.Metadata["Username"];
                    // Here you can save order and customer details to your database.
                    _logger.LogCritical(name);

                    Audit audit = new Audit()
                    {
                        SessionId = session.PaymentIntentId,
                    };
                    _auditRepository.AddAuditLog(audit);
                    await _auditRepository.SaveAllAsync();

                    int total = Convert.ToInt32(session.Amount / 100);

                    AppUser user = await _userRepository.GetUserByUsernameAsync(name);

                    user.Vote += total;

                    _userRepository.Update(user);

                    await _userRepository.SaveAllAsync();

                    string msg = $"You have voted {total} to {name}";

                    _logger.LogCritical("\n PaymentIntentId : {0} \n Payment Method : {1} \n Description: {2}",
                    session.PaymentIntentId,
                    session.PaymentMethodDetails,
                    msg);
                }
                return Ok();
            } 
            // ... handle other event types
            else
            {
                return Ok();
            }

        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex.Message);
            return BadRequest();
        }
    }

}
