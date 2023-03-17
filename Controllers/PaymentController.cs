using API.DTOs;
using API.Entities;
using API.Extensions;
using API.Interface;
using AutoMapper;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc;
using Stripe.Checkout;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentController : BaseApiController
{

    private readonly IUserRepository _userRepository;
    private readonly IConfiguration _configuration;

    private static string s_wasmClientURL = string.Empty;

    public PaymentController(IConfiguration configuration, IUserRepository userRepository)
    {
        _configuration = configuration;
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
                            Name = product.Title,
                            Description = product.Description,
                            Images = new List<string> { product.ImageUrl }
                        },
                    },
                    Quantity = 1,
                },
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
    public async Task<ActionResult> CheckoutSuccess(string sessionId, string name)
    {
        var sessionService = new SessionService();
        var session = sessionService.Get(sessionId);

        // Here you can save order and customer details to your database.
        int total = Convert.ToInt32(session.AmountTotal.Value / 100);
        var customerEmail = session.CustomerDetails.Email;

        //declare changes in user


        //call api to store voting
        AppUser user = await _userRepository.GetUserByUsernameAsync(name);
        user.Vote += total;

        _userRepository.Update(user);

        await _userRepository.SaveAllAsync();

        return Redirect(s_wasmClientURL);
    }
}
