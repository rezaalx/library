using System.Security.Cryptography;

namespace Minit.Api.Services;

public sealed class UserCodeGenerator : IUserCodeGenerator
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int CodeLength = 8;
    public string Generate()
    {
        return GenerateCode();
    }

    private static string GenerateCode()
    {
        Span<char> chars = stackalloc char[CodeLength];
        Span<byte> randomBytes = stackalloc byte[CodeLength];
        RandomNumberGenerator.Fill(randomBytes);

        for (var i = 0; i < CodeLength; i++)
        {
            chars[i] = Alphabet[randomBytes[i] % Alphabet.Length];
        }

        return new string(chars);
    }
}
