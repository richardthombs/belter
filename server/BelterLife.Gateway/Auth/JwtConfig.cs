namespace BelterLife.Gateway.Auth;

/// <summary>JWT configuration bound from IConfiguration section "Jwt".</summary>
public class JwtConfig
{
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int ExpiryHours { get; set; } = 24;
}
