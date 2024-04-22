using System.CommandLine.Help;
using System.Runtime.InteropServices;

static class OSFlavor
{
    public static bool IsFedoraLike => MatchesId("fedora");
    public static bool IsDebianLike => MatchesId("debian");

    private static string? _id;
    private static string? _idLike;

    public static void ThrowNotSupported()
    {
        EnsureIds();
        throw new NotSupportedException($"Can not determine location to install CA certificate on {RuntimeInformation.OSDescription}.");
    }

    private static bool MatchesId(string id)
    {
        EnsureIds();
        return _id == id || _idLike == id;
    }

    public static void EnsureIds()
    {
        if (_id is null || _idLike is null)
        {
            if (File.Exists("/etc/os-release"))
            {
                string[] lines = File.ReadAllLines("/etc/os-release");

                foreach (string line in lines)
                {
                    ReadOnlySpan<char> lineSpan = line.AsSpan();

                    ReadOnlySpan<char> value = default;
                    if(TryGetFieldValue(lineSpan, "ID=", ref value))
                    {
                        _id = value.ToString();
                    }
                    else if(TryGetFieldValue(lineSpan, "ID_LIKE=", ref value))
                    {
                        _idLike = value.ToString();
                    }
                }
            }
            _id ??= "";
            _idLike ??= "";
        }

        static bool TryGetFieldValue(ReadOnlySpan<char> line, ReadOnlySpan<char> prefix, ref ReadOnlySpan<char> value)
        {
            if (!line.StartsWith(prefix))
            {
                return false;
            }
            ReadOnlySpan<char> fieldValue = line.Slice(prefix.Length);

            // Remove enclosing quotes.
            if (fieldValue.Length >= 2 &&
                fieldValue[0] is '"' or '\'' &&
                fieldValue[0] == fieldValue[^1])
            {
                fieldValue = fieldValue[1..^1];
            }

            value = fieldValue;
            return true;
        }
    }
}