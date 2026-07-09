namespace SkyScan.Application.Account.Common
{
    public class EnableTwoFactorSetupResult
    {
        public string SharedKey { get; set; } = string.Empty;
        public string AuthenticatorUri { get; set; } = string.Empty;
    }
}
