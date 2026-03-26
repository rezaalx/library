using System.Security.Cryptography;

namespace Workspace.Services;

public interface ICodeGenerator
{
    string GenerateCode(int length = 8);
}

public sealed class CodeGenerator : ICodeGenerator
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public string GenerateCode(int length = 8)
    {
        if (length <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Code length must be greater than zero.");
        }

        Span<byte> randomBytes = stackalloc byte[length];
        RandomNumberGenerator.Fill(randomBytes);

        var chars = new char[length];
        for (var i = 0; i < length; i++)
        {
            chars[i] = Alphabet[randomBytes[i] % Alphabet.Length];
        }

        return new string(chars);
    }
}
