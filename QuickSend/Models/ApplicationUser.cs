using Microsoft.AspNetCore.Identity;

namespace QuickSend.Models
{
    public class ApplicationUser : IdentityUser
    {
        public decimal Balance { get; set; } = 0;
        public ICollection<WalletTransactions> walletTransactions { get; set; } = new List<WalletTransactions>();
    }
}
