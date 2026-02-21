namespace BelterLife.Gateway.Auth;

/// <summary>JWT configuration bound from IConfiguration (K8s Secret: JwtKey).</summary>
public class JwtConfig
{
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
}
