namespace GrowDaGarden;

/// <summary>Human-readable big-number formatting (1.2K, 50.0M, 5.00T, …).</summary>
public static class Num
{
    private static readonly string[] Suffix =
        { "", "K", "M", "B", "T", "Qa", "Qi", "Sx", "Sp", "Oc", "No", "Dc" };

    public static string Fmt(double n)
    {
        if (n < 0) return "-" + Fmt(-n);
        if (n < 1000) return ((long)n).ToString();

        double v = n;
        int s = 0;
        while (v >= 1000 && s < Suffix.Length - 1)
        {
            v /= 1000.0;
            s++;
        }
        string num = v < 10 ? v.ToString("0.00") : v < 100 ? v.ToString("0.0") : v.ToString("0");
        return num + Suffix[s];
    }
}
