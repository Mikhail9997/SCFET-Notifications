namespace Application.Common.Interfaces;

public interface IRandomTokenGenerator
{
    string GenerateToken(int length);
}