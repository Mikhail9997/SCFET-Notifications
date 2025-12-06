namespace Application.Configurations;

public class JwtSettings
{
    public string Secret { get; set; }
    public string Issuer { get; set; }
    public string Audience { get; set; }
    public int AccessTokenExpiryMinutes { get; set; } = 15;
    
    public int AccessTokenExpiryDays { get; set; } = 7;
}
