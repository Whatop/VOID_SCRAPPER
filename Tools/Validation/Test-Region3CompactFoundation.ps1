param(
    [ValidateRange(1, 100000)]
    [int]$SeedsPerCombination = 1000
)

$ErrorActionPreference = 'Stop'

$source = @'
using System;
using System.Collections.Generic;

public static class Region3CompactFoundationStress
{
    private const int SectorCount = 8;
    private const int TemplateCount = 6;
    private const int RequiredReflectors = 12;
    private const int CandidateCount = 16;
    private const double ExtentX = 24.5;
    private const double ExtentY = 27.78;
    private const double FootprintRadius = 2.75;
    private const double PlayerClearance = 8.0;
    private const double BossClearance = 8.0;
    private const double MinimumSpacing = 5.5;
    private const double MinimumSpan = 0.52;
    private static readonly double[] Scales = { 0.68, 0.62, 0.74 };

    private struct Point
    {
        public double X;
        public double Y;

        public Point(double x, double y)
        {
            X = x;
            Y = y;
        }
    }

    public static string Run(int seedsPerCombination)
    {
        Random random = new Random(31083);
        int samples = 0;
        int failures = 0;
        double minimumPlayerDistance = double.PositiveInfinity;
        double minimumBossDistance = double.PositiveInfinity;
        double minimumPlateSpacing = double.PositiveInfinity;
        double minimumSpanX = double.PositiveInfinity;
        double minimumSpanY = double.PositiveInfinity;

        for (int sector = 0; sector < SectorCount; sector++)
        {
            for (int template = 0; template < TemplateCount; template++)
            {
                for (int seed = 0; seed < seedsPerCombination; seed++)
                {
                    Point player = CreatePlayerStart(random, sector);
                    Point boss = ResolveBossPosition(player);
                    Point[] reflectors;
                    double playerDistance;
                    double bossDistance;
                    double spacing;
                    double spanX;
                    double spanY;
                    samples++;

                    if (!TryBuildFoundation(
                            player,
                            boss,
                            out reflectors,
                            out playerDistance,
                            out bossDistance,
                            out spacing,
                            out spanX,
                            out spanY))
                    {
                        failures++;
                        continue;
                    }

                    minimumPlayerDistance = Math.Min(minimumPlayerDistance, playerDistance);
                    minimumBossDistance = Math.Min(minimumBossDistance, bossDistance);
                    minimumPlateSpacing = Math.Min(minimumPlateSpacing, spacing);
                    minimumSpanX = Math.Min(minimumSpanX, spanX);
                    minimumSpanY = Math.Min(minimumSpanY, spanY);
                }
            }
        }

        Point regressionPlayer = new Point(-16.32, 3.42);
        Point regressionBoss = ResolveBossPosition(regressionPlayer);
        Point[] regressionReflectors;
        double regressionPlayerDistance;
        double regressionBossDistance;
        double regressionSpacing;
        double regressionSpanX;
        double regressionSpanY;
        samples++;
        if (!TryBuildFoundation(
                regressionPlayer,
                regressionBoss,
                out regressionReflectors,
                out regressionPlayerDistance,
                out regressionBossDistance,
                out regressionSpacing,
                out regressionSpanX,
                out regressionSpanY))
        {
            failures++;
        }
        else
        {
            minimumPlayerDistance = Math.Min(minimumPlayerDistance, regressionPlayerDistance);
            minimumBossDistance = Math.Min(minimumBossDistance, regressionBossDistance);
            minimumPlateSpacing = Math.Min(minimumPlateSpacing, regressionSpacing);
            minimumSpanX = Math.Min(minimumSpanX, regressionSpanX);
            minimumSpanY = Math.Min(minimumSpanY, regressionSpanY);
        }

        return
            "[Region3 Compact Foundation Stress] " +
            "Samples=" + samples +
            " Failures=" + failures +
            " Reflectors=" + RequiredReflectors +
            " MinPlayerClearance=" + minimumPlayerDistance.ToString("0.###") +
            " MinBossClearance=" + minimumBossDistance.ToString("0.###") +
            " MinSpacing=" + minimumPlateSpacing.ToString("0.###") +
            " MinSpan=" + minimumSpanX.ToString("0.###") + "x" +
            minimumSpanY.ToString("0.###");
    }

    private static Point CreatePlayerStart(Random random, int sector)
    {
        double insetX = 8.0 + random.NextDouble() * 4.0;
        double insetY = 8.0 + random.NextDouble() * 4.0;
        double lateralX = (random.NextDouble() * 2.0 - 1.0) * ExtentX * 0.45;
        double lateralY = (random.NextDouble() * 2.0 - 1.0) * ExtentY * 0.45;

        switch (sector)
        {
            case 0: return new Point(lateralX, ExtentY - insetY);
            case 1: return new Point(ExtentX - insetX, ExtentY - insetY);
            case 2: return new Point(ExtentX - insetX, lateralY);
            case 3: return new Point(ExtentX - insetX, -ExtentY + insetY);
            case 4: return new Point(lateralX, -ExtentY + insetY);
            case 5: return new Point(-ExtentX + insetX, -ExtentY + insetY);
            case 6: return new Point(-ExtentX + insetX, lateralY);
            default: return new Point(-ExtentX + insetX, ExtentY - insetY);
        }
    }

    private static Point ResolveBossPosition(Point player)
    {
        double length = Math.Sqrt(player.X * player.X + player.Y * player.Y);
        double directionX = length > 0.0001 ? -player.X / length : 1.0;
        double directionY = length > 0.0001 ? -player.Y / length : 0.0;
        Point best = new Point(0.0, 0.0);
        double bestDistanceSquared = -1.0;

        for (int i = 0; i < 8; i++)
        {
            double angle = i == 0
                ? 0.0
                : (i % 2 == 1 ? 1.0 : -1.0) * ((i + 1) / 2) * 22.5;
            double radians = angle * Math.PI / 180.0;
            double rotatedX = directionX * Math.Cos(radians) - directionY * Math.Sin(radians);
            double rotatedY = directionX * Math.Sin(radians) + directionY * Math.Cos(radians);
            Point candidate = new Point(
                rotatedX * ExtentX * 0.24,
                rotatedY * ExtentY * 0.24
            );
            double distanceSquared = DistanceSquared(candidate, player);
            if (distanceSquared >= 18.0 * 18.0 && distanceSquared > bestDistanceSquared)
            {
                best = candidate;
                bestDistanceSquared = distanceSquared;
            }
        }

        return bestDistanceSquared >= 0.0
            ? best
            : new Point(directionX * ExtentX * 0.24, directionY * ExtentY * 0.24);
    }

    private static bool TryBuildFoundation(
        Point player,
        Point boss,
        out Point[] result,
        out double minimumPlayerDistance,
        out double minimumBossDistance,
        out double minimumSpacing,
        out double normalizedSpanX,
        out double normalizedSpanY)
    {
        result = null;
        minimumPlayerDistance = double.PositiveInfinity;
        minimumBossDistance = double.PositiveInfinity;
        minimumSpacing = double.PositiveInfinity;
        normalizedSpanX = 0.0;
        normalizedSpanY = 0.0;

        double playerLength = Math.Sqrt(player.X * player.X + player.Y * player.Y);
        double awayX = playerLength > 0.0001 ? -player.X / playerLength : 0.0;
        double awayY = playerLength > 0.0001 ? -player.Y / playerLength : 0.0;

        for (int layoutAttempt = 0; layoutAttempt < 12; layoutAttempt++)
        {
            double scale = Scales[layoutAttempt % Scales.Length];
            int centerVariant = layoutAttempt / Scales.Length;
            double centerX = centerVariant == 1
                ? awayX * 2.5
                : centerVariant == 2 ? -awayY * 2.5 : centerVariant == 3 ? awayY * 2.5 : 0.0;
            double centerY = centerVariant == 1
                ? awayY * 2.5
                : centerVariant == 2 ? awayX * 2.5 : centerVariant == 3 ? -awayX * 2.5 : 0.0;
            double layoutExtentX = (ExtentX - FootprintRadius) * scale;
            double layoutExtentY = (ExtentY - FootprintRadius) * scale;
            Point[] candidates = new Point[CandidateCount];
            bool[] available = new bool[CandidateCount];
            Point[] selected = new Point[RequiredReflectors];
            int availableMask = 0;

            for (int i = 0; i < CandidateCount; i++)
            {
                candidates[i] = ResolvePerimeterCandidate(i, layoutExtentX, layoutExtentY);
                candidates[i].X += centerX;
                candidates[i].Y += centerY;
                available[i] = Distance(candidates[i], player) >= PlayerClearance &&
                    Distance(candidates[i], boss) >= BossClearance &&
                    Math.Abs(candidates[i].X) <= ExtentX - FootprintRadius &&
                    Math.Abs(candidates[i].Y) <= ExtentY - FootprintRadius;
                if (available[i])
                {
                    availableMask |= 1 << i;
                }
            }

            int bestMask = -1;
            double bestScore = double.NegativeInfinity;
            int fullMask = (1 << CandidateCount) - 1;
            for (int omittedA = 0; omittedA < CandidateCount - 3; omittedA++)
            {
                for (int omittedB = omittedA + 1; omittedB < CandidateCount - 2; omittedB++)
                {
                    for (int omittedC = omittedB + 1; omittedC < CandidateCount - 1; omittedC++)
                    {
                        for (int omittedD = omittedC + 1; omittedD < CandidateCount; omittedD++)
                        {
                            int omittedMask = (1 << omittedA) | (1 << omittedB) |
                                (1 << omittedC) | (1 << omittedD);
                            int mask = fullMask & ~omittedMask;
                            if ((mask & ~availableMask) != 0 ||
                                !SelectionIsDistributed(mask, candidates))
                            {
                                continue;
                            }

                            double score = 0.0;
                            for (int i = 0; i < CandidateCount; i++)
                            {
                                if ((mask & (1 << i)) == 0)
                                {
                                    continue;
                                }

                                score += DistanceSquared(candidates[i], player) +
                                    DistanceSquared(candidates[i], boss) * 0.1;
                            }

                            if (score > bestScore)
                            {
                                bestScore = score;
                                bestMask = mask;
                            }
                        }
                    }
                }
            }

            int selectedCount = 0;
            for (int i = 0; i < CandidateCount && bestMask >= 0; i++)
            {
                if ((bestMask & (1 << i)) != 0)
                {
                    selected[selectedCount++] = candidates[i];
                }
            }

            if (selectedCount != RequiredReflectors ||
                !Validate(
                    selected,
                    player,
                    boss,
                    out minimumPlayerDistance,
                    out minimumBossDistance,
                    out minimumSpacing,
                    out normalizedSpanX,
                    out normalizedSpanY))
            {
                continue;
            }

            result = selected;
            return true;
        }

        return false;
    }

    private static bool SelectionIsDistributed(int mask, Point[] candidates)
    {
        double minX = double.PositiveInfinity;
        double maxX = double.NegativeInfinity;
        double minY = double.PositiveInfinity;
        double maxY = double.NegativeInfinity;
        int quadrantMask = 0;

        for (int i = 0; i < CandidateCount; i++)
        {
            if ((mask & (1 << i)) == 0)
            {
                continue;
            }

            Point point = candidates[i];
            minX = Math.Min(minX, point.X);
            maxX = Math.Max(maxX, point.X);
            minY = Math.Min(minY, point.Y);
            maxY = Math.Max(maxY, point.Y);
            int quadrant = point.X >= 0.0
                ? (point.Y >= 0.0 ? 0 : 3)
                : (point.Y >= 0.0 ? 1 : 2);
            quadrantMask |= 1 << quadrant;

            for (int other = 0; other < i; other++)
            {
                if ((mask & (1 << other)) != 0 &&
                    Distance(point, candidates[other]) < MinimumSpacing)
                {
                    return false;
                }
            }
        }

        double spanX = (maxX - minX) / (ExtentX * 2.0);
        double spanY = (maxY - minY) / (ExtentY * 2.0);
        return quadrantMask == 0x0F && spanX >= MinimumSpan && spanY >= MinimumSpan;
    }

    private static Point ResolvePerimeterCandidate(int index, double extentX, double extentY)
    {
        int side = index / 4;
        double t = (index % 4) * 0.25;
        switch (side)
        {
            case 0: return new Point(Lerp(-extentX, extentX, t), extentY);
            case 1: return new Point(extentX, Lerp(extentY, -extentY, t));
            case 2: return new Point(Lerp(extentX, -extentX, t), -extentY);
            default: return new Point(-extentX, Lerp(-extentY, extentY, t));
        }
    }

    private static bool Validate(
        Point[] points,
        Point player,
        Point boss,
        out double playerDistance,
        out double bossDistance,
        out double spacing,
        out double spanX,
        out double spanY)
    {
        playerDistance = double.PositiveInfinity;
        bossDistance = double.PositiveInfinity;
        spacing = double.PositiveInfinity;
        double minX = double.PositiveInfinity;
        double maxX = double.NegativeInfinity;
        double minY = double.PositiveInfinity;
        double maxY = double.NegativeInfinity;
        int quadrantMask = 0;

        for (int i = 0; i < points.Length; i++)
        {
            Point point = points[i];
            if (Math.Abs(point.X) > ExtentX - FootprintRadius ||
                Math.Abs(point.Y) > ExtentY - FootprintRadius)
            {
                spanX = 0.0;
                spanY = 0.0;
                return false;
            }

            playerDistance = Math.Min(playerDistance, Distance(point, player));
            bossDistance = Math.Min(bossDistance, Distance(point, boss));
            minX = Math.Min(minX, point.X);
            maxX = Math.Max(maxX, point.X);
            minY = Math.Min(minY, point.Y);
            maxY = Math.Max(maxY, point.Y);

            int quadrant = point.X >= 0.0
                ? (point.Y >= 0.0 ? 0 : 3)
                : (point.Y >= 0.0 ? 1 : 2);
            quadrantMask |= 1 << quadrant;

            for (int other = 0; other < i; other++)
            {
                spacing = Math.Min(spacing, Distance(point, points[other]));
            }
        }

        spanX = (maxX - minX) / (ExtentX * 2.0);
        spanY = (maxY - minY) / (ExtentY * 2.0);
        return playerDistance >= PlayerClearance &&
            bossDistance >= BossClearance &&
            spacing >= MinimumSpacing &&
            spanX >= MinimumSpan &&
            spanY >= MinimumSpan &&
            quadrantMask == 0x0F;
    }

    private static bool HasSpacing(Point candidate, Point[] selected, int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (Distance(candidate, selected[i]) < MinimumSpacing)
            {
                return false;
            }
        }

        return true;
    }

    private static double Distance(Point first, Point second)
    {
        return Math.Sqrt(DistanceSquared(first, second));
    }

    private static double DistanceSquared(Point first, Point second)
    {
        double x = first.X - second.X;
        double y = first.Y - second.Y;
        return x * x + y * y;
    }

    private static double Lerp(double start, double end, double t)
    {
        return start + (end - start) * t;
    }
}
'@

Add-Type -TypeDefinition $source -Language CSharp
$result = [Region3CompactFoundationStress]::Run($SeedsPerCombination)
Write-Output $result

if ($result -notmatch 'Failures=0(?:\s|$)')
{
    throw "Region-3 compact reflector foundation stress validation failed."
}
