using System.Security.Cryptography;
using System.Text;

namespace LocationSharing.Api.Utilities;

public static class TripCodeGenerator
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static string Generate(int length = 8)
    {
        var bytes = RandomNumberGenerator.GetBytes(length);
        var builder = new StringBuilder(length);

        foreach (var value in bytes)
        {
            builder.Append(Alphabet[value % Alphabet.Length]);
        }

        return builder.ToString();
    }
}
