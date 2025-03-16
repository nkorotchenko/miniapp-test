namespace WebAppBot.Config
{
    public class BotConfig
    {
        public string? Token { get; set; }

        public Uri? PublicUrl { get; set; }

        public bool NotConfigured => Token == null || String.IsNullOrWhiteSpace(Token) || PublicUrl == null;
    }
}
