using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuickSend.Data;
using QuickSend.Models;
using Stripe;

namespace QuickSend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WalletController : ControllerBase
    {
        
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public WalletController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }


        //  I’m not following best practices for this controller

        [HttpPost("send")]
        public async Task<IActionResult> SendMoney([FromBody] SendMoneyDto dto)
        {
            if (dto.Amount <= 0)
                return BadRequest("Amount must be greater than zero.");

            var sender = await _userManager.GetUserAsync(User);
            if (sender == null)
                return NotFound("Sender not found.");

            var recipient = await _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == dto.SenderUserPhoneNumber);
            if (recipient == null)
                return NotFound("Recipient not found.");

            if (sender.Balance < dto.Amount)
                return BadRequest("Insufficient balance.");

            sender.Balance -= dto.Amount;
            recipient.Balance += dto.Amount;

            var senderTx = new WalletTransactions
            {
                UserId = sender.Id,
                transactionType = TransactionType.send,
                Amount = dto.Amount
            };

            var recipientTx = new WalletTransactions
            {
                UserId = recipient.Id,
                transactionType = TransactionType.recieved,
                Amount = dto.Amount,
            };

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _context.Users.Update(sender);
                _context.Users.Update(recipient);
                
                await _context.WalletTransactions.AddAsync(senderTx);
                await _context.WalletTransactions.AddAsync(recipientTx);

                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return Ok(new { message = "Money sent successfully." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }

        }

        [HttpPost("deposit")]
        public async Task<IActionResult> Deposit([FromBody] DepositDto dto)
        {
            var user = await _userManager.GetUserAsync(User);
                if (user == null)
                return NotFound("User not found.");

            if (dto.AmountInCents <= 0)
                return BadRequest("Amount must be greater than zero.");

            var paymentIntentService = new PaymentIntentService();
            PaymentIntent paymentIntent;

            try
            {
                var options = new PaymentIntentCreateOptions
                {
                    Amount = dto.AmountInCents,
                    Currency = dto.Currency,
                    PaymentMethod = dto.PaymentMethodId,
                    ConfirmationMethod = "manual",
                    Confirm = true
                };
                paymentIntent = await paymentIntentService.CreateAsync(options);
            }
            catch (StripeException ex)
            {
                return BadRequest(new { error = ex.Message });
            }

            if (paymentIntent.Status == "requires_action" || paymentIntent.Status == "requires_source_action")
            {
                return Ok(new
                {
                    requiresAction = true,
                    paymentIntentClientSecret = paymentIntent.ClientSecret
                });
            }
            else if (paymentIntent.Status == "succeeded")
            {
                decimal amountDecimal = dto.AmountInCents / 100m;
                user.Balance += amountDecimal;

                var depositTx = new WalletTransactions
                {
                    UserId = user.Id,
                    transactionType = TransactionType.Deposit,
                    Amount = amountDecimal
                };

                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    _context.Users.Update(user);
                    await _context.WalletTransactions.AddAsync(depositTx);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return StatusCode(500, $"Internal server error: {ex.Message}");
                }

                return Ok(new { message = "Deposit successful.", balance = user.Balance });
            }
            else
            {
                return BadRequest(new { error = "Payment failed." });
            }
        }


        // need to handle StripeConnectedAccountId

        //[HttpPost("withdraw")]
        //public async Task<IActionResult> Withdraw([FromBody] WithdrawDto dto)
        //{
        //    // 1. Validate amount and check balance
        //    var user = await _userManager.GetUserAsync(User);
        //    if (user == null) return NotFound("User not found");
        //    if (user.Balance < dto.Amount) return BadRequest("Insufficient balance");

        //    user.Balance -= dto.Amount;
        //    _context.Users.Update(user);

        //    var withdrawTx = new WalletTransactions
        //    {
        //        UserId = user.Id,
        //        transactionType = TransactionType.withdraw,
        //        Amount = dto.Amount
        //    };

        //    await _context.WalletTransactions.AddAsync(withdrawTx);

        //    // 2. Save DB changes first
        //    await _context.SaveChangesAsync();

        //    // 3. Call Stripe payout
        //    bool payoutResult = await SendStripePayoutAsync(user.StripeConnectedAccountId, (long)(dto.Amount * 100), "usd");

        //    if (!payoutResult)
        //    {
        //        // Rollback or handle failure (refund balance, mark transaction failed, etc.)
        //        return StatusCode(500, "Payout failed. Please contact support.");
        //    }

        //    return Ok(new { message = "Withdrawal successful", balance = user.Balance });
        //}
        //public async Task<bool> SendStripePayoutAsync(string connectedAccountId, long amountInCents, string currency)
        //{
        //    StripeConfiguration.ApiKey = "sk_test_YourSecretKey";

        //    var payoutService = new PayoutService();

        //    var options = new PayoutCreateOptions
        //    {
        //        Amount = amountInCents,
        //        Currency = currency,
        //    };

        //    var requestOptions = new RequestOptions
        //    {
        //        StripeAccount = connectedAccountId
        //    };

        //    try
        //    {
        //        var payout = await payoutService.CreateAsync(options, requestOptions);
        //        // Payout created successfully
        //        return true;
        //    }
        //    catch (StripeException ex)
        //    {
        //        // Handle error
        //        Console.WriteLine(ex.Message);
        //        return false;
        //    }
        //}


        public class SendMoneyDto
        {
            public string SenderUserPhoneNumber { get; set; }
            public decimal Amount { get; set; }
        }
        public class DepositDto
        {
            public long AmountInCents { get; set; }  // Stripe uses smallest currency unit (e.g., cents)
            public string Currency { get; set; } = "usd"; // default to USD, change as needed
            public string PaymentMethodId { get; set; }   // Stripe Payment Method ID from frontend
        }
        public class WithdrawDto
        {
            public decimal Amount { get; set; }
        }


    }
}
