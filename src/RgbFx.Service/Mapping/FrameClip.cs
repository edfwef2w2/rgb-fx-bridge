namespace RgbFx.Service.Mapping;

public static class FrameClip
{
    public static string[] Even(IReadOnlyList<string> src, int dest)
    {
        dest = Math.Max(1, dest);
        if (src.Count == dest)
            return src as string[] ?? src.ToArray();
        if (src.Count == 0)
        {
            var blank = new string[dest];
            Array.Fill(blank, "000000");
            return blank;
        }

        if (src.Count < dest)
        {
            var padded = new string[dest];
            for (var i = 0; i < dest; i++)
                padded[i] = i < src.Count ? src[i] : src[^1];
            return padded;
        }

        var nIn = src.Count;
        var clipped = new string[dest];
        for (var i = 0; i < dest; i++)
            clipped[i] = src[(int)((long)i * nIn / dest)];
        return clipped;
    }
}
