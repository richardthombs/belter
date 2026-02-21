namespace BelterLife.Gateway.Infrastructure;

/// <summary>
/// Tracks JWT tokens that have been explicitly invalidated via logout.
/// Rows are safe to delete after ExpiresAt to prevent unbounded table growth.
/// </summary>
public class RevokedToken
{
    /// <summary>JWT ID claim (jti) — unique per token, used as the primary key.</summary>
    public string Jti { get; set; } = string.Empty;

    /// <summary>When the original token expires — safe to purge rows after this time.</summary>
    public DateTimeOffset ExpiresAt { get; set; }
}
