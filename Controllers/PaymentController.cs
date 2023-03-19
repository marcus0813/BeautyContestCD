using API.DTOs;
using API.Entities;
using API.Interface;
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

    private static string s_wasmClientURL = string.Empty;

    public PaymentController(IConfiguration configuration, ILogger<PaymentController> logger,IUserRepository userRepository)
    {
        _configuration = configuration;
        _logger = logger;
        _userRepository = userRepository;
        
    }

    [HttpPost]
    public async Task<ActionResult> CheckoutOrder([FromBody] Vote product, [FromServices] IServiceProvider sp)
    {
        var referer = Request.Headers.Referer;
        s_wasmClientURL = referer[0];

        // Build the URL to which the customer will be redirected after paying.
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
        // Create a payment flow from the items in the cart.
        // Gets sent to Stripe API.

        Stripe.StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];

        string description = "You have voted " + product.Price / 100 + " to " + product.Description;

        var options = new SessionCreateOptions
        {
            // Stripe calls the URLs below when certain checkout events happen such as success and failure.
            SuccessUrl = $"{thisApiUrl}/api/payment/success?sessionId=" + "{CHECKOUT_SESSION_ID}" + $"&name={product.Title}", // Customer paid.
            CancelUrl = s_wasmClientURL,  // Checkout cancelled.
            PaymentMethodTypes = new List<string> // Only card available in test mode?
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
                            // Images = new List<string> { product.ImageUrl }
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
                }
            },
            Mode = "payment" // One-time payment. Stripe supports recurring 'subscription' payments.
        };

        var service = new SessionService();
        var session = await service.CreateAsync(options);

        return session.Id;
    }

    [HttpGet("success")]
    // Automatic query parameter handling from ASP.NET.
    // Example URL: https://localhost:7051/checkout/success?sessionId=si_123123123123
    public ActionResult CheckoutSuccess(string sessionId, string name)
    {
        return Redirect(s_wasmClientURL);
    }

    [HttpPost("webhook")]
    public async Task<ActionResult> WebhookHandler() 
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
        try
        {
            var stripeEvent = EventUtility.ConstructEvent(json,
                Request.Headers["Stripe-Signature"], _configuration["Stripe:WebHookKey"]);

            // Handle the event
            if(stripeEvent.Type == Events.ChargeSucceeded)
            {
                var session = stripeEvent.Data.Object as Charge;

                if (session.Status == "succeeded")
                {
                    // Here you can save order and customer details to your database.
                    int total = Convert.ToInt32(session.Amount / 100);
                    // var customerEmail = session.CustomerDetails.Email;

                    //call api to store voting
                    AppUser user = await _userRepository.GetUserByUsernameAsync(session.Metadata["Username"]);

                    user.Vote += total;

                    _userRepository.Update(user);

                    await _userRepository.SaveAllAsync();

                    _logger.LogCritical("\n PaymentIntentId : {0} \n Payment Method : {1} \n Description: {2}",
                    session.PaymentIntentId,
                    session.PaymentMethodDetails.Type,
                    session.Metadata["Description"]
                    );
                }

            } 
            // ... handle other event types
            else
            {

            }

            return Ok();
        }
        catch
        {
            return BadRequest();
        }
    }

}
