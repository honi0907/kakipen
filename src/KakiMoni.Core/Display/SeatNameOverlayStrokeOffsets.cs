namespace KakiMoni.Core.Display;

public static class SeatNameOverlayStrokeOffsets
{
    public static IReadOnlyList<(double X, double Y)> ForThickness(double thickness)
    {
        if (thickness <= 0)
            return Array.Empty<(double X, double Y)>();

        var radius = Math.Max(1, (int)Math.Ceiling(thickness));
        var offsets = new List<(double X, double Y)>();

        for (var ring = 1; ring <= radius; ring++)
        {
            for (var dx = -ring; dx <= ring; dx++)
            {
                for (var dy = -ring; dy <= ring; dy++)
                {
                    if (dx == 0 && dy == 0)
                        continue;

                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) == ring)
                        offsets.Add((dx, dy));
                }
            }
        }

        return offsets;
    }
}
