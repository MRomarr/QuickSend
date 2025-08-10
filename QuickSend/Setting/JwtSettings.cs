namespace QuickSend.Setting
{
    public class JwtSettings
    {
        public const string Section = "JwtSettings";
        public string Issuer { get; set; } = default!;
        public string Audience { get; set; } = default!;
        public string Secret { get; set; } = default!;
        public int ExpirationInMinutes { get; set; }
    }
}
