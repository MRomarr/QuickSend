namespace QuickSend.Models
{
    public class WalletTransactions
    {
        public string Id { get; set; }= Guid.NewGuid().ToString();
        
        public decimal Amount { get; set; }
        public TransactionType transactionType { get; set; }
        public string UserId { get; set; }
        
    }
    public enum TransactionType
    {
        Deposit,
        withdraw,
        send,
        recieved,
    }
}