using Godot;
using System.Text;

namespace GrowDaGarden;

/// <summary>LAN networking helpers: pick the local IP and turn it into a short
/// room code that friends type in to join (the code just encodes the host's IPv4).</summary>
public static class Net
{
    public const int Port = 24565;
    private const string Alpha = "0123456789ABCDEFGHJKMNPQRSTVWXYZ"; // 32 chars, no I/L/O/U

    /// <summary>Best-guess LAN IPv4 address of this machine.</summary>
    public static string LocalIp()
    {
        string best = "127.0.0.1";
        int bestRank = -1;
        foreach (string a in IP.GetLocalAddresses())
        {
            if (!IsIPv4(a) || a.StartsWith("127.") || a.StartsWith("169.254."))
                continue;
            int rank = a.StartsWith("192.168.") ? 3 : a.StartsWith("10.") ? 2 : IsPrivate172(a) ? 1 : 0;
            if (rank > bestRank) { bestRank = rank; best = a; }
        }
        return best;
    }

    /// <summary>Encode an IPv4 as a 7-char room code, shown grouped as ABCD-EFG.</summary>
    public static string Encode(string ip)
    {
        uint v = IpToUint(ip);
        var sb = new StringBuilder();
        for (int i = 6; i >= 0; i--)
            sb.Append(Alpha[(int)((v >> (i * 5)) & 0x1F)]);
        string s = sb.ToString();
        return s.Substring(0, 4) + "-" + s.Substring(4);
    }

    /// <summary>Decode a room code back to an IPv4 (empty string if invalid).</summary>
    public static string Decode(string code)
    {
        string s = code.Replace("-", "").Replace(" ", "").Trim().ToUpper();
        if (s.Length != 7) return "";
        uint v = 0;
        foreach (char c in s)
        {
            int idx = Alpha.IndexOf(c);
            if (idx < 0) return "";
            v = (v << 5) | (uint)idx;
        }
        return UintToIp(v);
    }

    private static uint IpToUint(string ip)
    {
        string[] p = ip.Split('.');
        return ((uint)int.Parse(p[0]) << 24) | ((uint)int.Parse(p[1]) << 16)
             | ((uint)int.Parse(p[2]) << 8) | (uint)int.Parse(p[3]);
    }

    private static string UintToIp(uint v) =>
        $"{(v >> 24) & 0xFF}.{(v >> 16) & 0xFF}.{(v >> 8) & 0xFF}.{v & 0xFF}";

    private static bool IsIPv4(string a)
    {
        string[] p = a.Split('.');
        if (p.Length != 4) return false;
        foreach (string x in p)
            if (!int.TryParse(x, out int n) || n < 0 || n > 255) return false;
        return true;
    }

    private static bool IsPrivate172(string a)
    {
        string[] p = a.Split('.');
        return p.Length >= 2 && p[0] == "172" && int.TryParse(p[1], out int n) && n >= 16 && n <= 31;
    }
}
