using System.Security.Cryptography;
using Application.Common.Interfaces;

namespace Application.Common.Services;

public class RandomTokenGenerator : IRandomTokenGenerator
{
    public string GenerateToken(int length)
    {
        var randomNumber = new byte[length];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }
}