using System.CommandLine;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Themes.Simple;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Maestro.Tools.Commands;

public static class VisualizeCommand
{
    public static Command Build()
    {
        var configOption = new Option<FileInfo>("--config", "Path to Maestro.yaml") { IsRequired = true };
        var command = new Command("visualize", "Visualize trajectory segments from a Maestro.yaml file.");
        command.AddOption(configOption);

        command.SetHandler(configFile =>
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(NullNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var root = deserializer.Deserialize<YamlRoot>(File.ReadAllText(configFile.FullName));
            var trajectories = root.Airports
                .SelectMany(a => a.TerminalTrajectories.Select(t => new TrajectoryEntry(a.Identifier, t)))
                .ToList();

            AppBuilder.Configure(() => new VisualizerApp(trajectories))
                .UsePlatformDetect()
                .StartWithClassicDesktopLifetime([]);
        }, configOption);

        return command;
    }
}

// ── YAML models ──────────────────────────────────────────────────────────────

class YamlRoot
{
    public List<YamlAirport> Airports { get; set; } = [];
}

class YamlAirport
{
    public string Identifier { get; set; } = "";
    public List<YamlTrajectory> TerminalTrajectories { get; set; } = [];
}

class YamlTrajectory
{
    public string FeederFix { get; set; } = "";
    public string TransitionFix { get; set; } = "";
    public string? ApproachType { get; set; }
    public string RunwayIdentifier { get; set; } = "";
    public List<YamlSegment> Segments { get; set; } = [];
    public YamlBranch? Pressure { get; set; }
    public YamlBranch? MaxPressure { get; set; }
}

class YamlSegment
{
    public string Identifier { get; set; } = "";
    public double Track { get; set; }
    public double DistanceNM { get; set; }
}

class YamlBranch
{
    public string After { get; set; } = "";
    public List<YamlSegment> Segments { get; set; } = [];
}

// ── View model ───────────────────────────────────────────────────────────────

record TrajectoryEntry(string Airport, YamlTrajectory Trajectory)
{
    public override string ToString()
    {
        var s = $"{Airport} {Trajectory.FeederFix} -> {Trajectory.RunwayIdentifier}";
        if (!string.IsNullOrEmpty(Trajectory.TransitionFix))
            s += $" via {Trajectory.TransitionFix}";
        if (!string.IsNullOrEmpty(Trajectory.ApproachType))
            s += $" ({Trajectory.ApproachType})";
        return s;
    }
}

// ── Avalonia application ─────────────────────────────────────────────────────

class VisualizerApp(List<TrajectoryEntry> trajectories) : Application
{
    public override void Initialize() => Styles.Add(new SimpleTheme());

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new VisualizerWindow(trajectories);
        base.OnFrameworkInitializationCompleted();
    }
}

class VisualizerWindow : Window
{
    private readonly TrajectoryCanvas _canvas = new();

    public VisualizerWindow(List<TrajectoryEntry> trajectories)
    {
        Title = "Trajectory Visualizer";
        Width = 1200;
        Height = 750;

        var listBox = new ListBox { ItemsSource = trajectories };
        listBox.SelectionChanged += (_, _) =>
            _canvas.Trajectory = listBox.SelectedItem as TrajectoryEntry;

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("300,*") };
        Grid.SetColumn(listBox, 0);
        Grid.SetColumn(_canvas, 1);
        grid.Children.Add(listBox);
        grid.Children.Add(_canvas);

        Content = grid;
    }
}

// ── Drawing control ──────────────────────────────────────────────────────────

class TrajectoryCanvas : Control
{
    private TrajectoryEntry? _entry;

    public TrajectoryEntry? Trajectory
    {
        get => _entry;
        set { _entry = value; InvalidateVisual(); }
    }

    public override void Render(DrawingContext ctx)
    {
        ctx.DrawRectangle(Brushes.Black, null, new Rect(Bounds.Size));

        if (_entry is null)
            return;

        var t = _entry.Trajectory;

        var (normalPts, waypointPos) = TraceNormalPath(t);

        List<Vec2> pressurePts = [];
        Vec2? pressureOrigin = null;
        if (t.Pressure is { } p && waypointPos.TryGetValue(p.After, out var po))
        {
            pressureOrigin = po;
            pressurePts = TraceBranch(po, p.Segments);
        }

        List<Vec2> maxPressurePts = [];
        Vec2? maxPressureOrigin = null;
        if (t.MaxPressure is { } mp && waypointPos.TryGetValue(mp.After, out var mpo))
        {
            maxPressureOrigin = mpo;
            maxPressurePts = TraceBranch(mpo, mp.Segments);
        }

        var allPts = normalPts.Concat(pressurePts).Concat(maxPressurePts).ToList();
        var xform = FitTransform(allPts, Bounds.Width, Bounds.Height, padding: 50);

        // Normal path
        DrawPolyline(ctx, new Pen(Brushes.LimeGreen, 2), normalPts, xform);

        // Pressure branch
        if (pressureOrigin.HasValue && pressurePts.Count > 0)
        {
            var pts = new List<Vec2> { pressureOrigin.Value };
            pts.AddRange(pressurePts);
            DrawPolyline(ctx, new Pen(new SolidColorBrush(Color.FromRgb(255, 165, 0)), 2), pts, xform);
        }

        // Max-pressure branch
        if (maxPressureOrigin.HasValue && maxPressurePts.Count > 0)
        {
            var pts = new List<Vec2> { maxPressureOrigin.Value };
            pts.AddRange(maxPressurePts);
            DrawPolyline(ctx, new Pen(Brushes.Red, 2), pts, xform);
        }

        // Labels along normal path
        var typeface = Typeface.Default;
        var labelBrush = Brushes.White;
        DrawLabel(ctx, t.FeederFix, normalPts[0], xform, typeface, labelBrush);
        for (var i = 0; i < t.Segments.Count; i++)
            DrawLabel(ctx, t.Segments[i].Identifier, normalPts[i + 1], xform, typeface, labelBrush);
    }

    // Walks the normal segments, returning all waypoint positions starting with the feeder fix at origin.
    // Also returns a lookup by segment identifier so branch points can be located.
    static (List<Vec2> Points, Dictionary<string, Vec2> Positions) TraceNormalPath(YamlTrajectory t)
    {
        var pts = new List<Vec2> { new(0, 0) };
        var pos = new Dictionary<string, Vec2>(StringComparer.OrdinalIgnoreCase);
        var x = 0.0;
        var y = 0.0;
        foreach (var seg in t.Segments)
        {
            var rad = seg.Track * Math.PI / 180.0;
            x += seg.DistanceNM * Math.Sin(rad);
            y -= seg.DistanceNM * Math.Cos(rad);
            var p = new Vec2(x, y);
            pts.Add(p);
            pos[seg.Identifier] = p;
        }
        return (pts, pos);
    }

    static List<Vec2> TraceBranch(Vec2 origin, List<YamlSegment> segments)
    {
        var pts = new List<Vec2>();
        var x = origin.X;
        var y = origin.Y;
        foreach (var seg in segments)
        {
            var rad = seg.Track * Math.PI / 180.0;
            x += seg.DistanceNM * Math.Sin(rad);
            y -= seg.DistanceNM * Math.Cos(rad);
            pts.Add(new Vec2(x, y));
        }
        return pts;
    }

    // Computes a uniform scale and (offsetX, offsetY) so all points fit within the canvas.
    static (double Scale, double OffsetX, double OffsetY) FitTransform(
        List<Vec2> pts, double width, double height, double padding)
    {
        if (pts.Count == 0) return (1, padding, padding);

        var minX = pts.Min(p => p.X);
        var maxX = pts.Max(p => p.X);
        var minY = pts.Min(p => p.Y);
        var maxY = pts.Max(p => p.Y);

        var rangeX = Math.Max(maxX - minX, 1);
        var rangeY = Math.Max(maxY - minY, 1);

        var usableW = width - padding * 2;
        var usableH = height - padding * 2;
        var scale = Math.Min(usableW / rangeX, usableH / rangeY);

        var offsetX = padding + (usableW - rangeX * scale) / 2.0 - minX * scale;
        var offsetY = padding + (usableH - rangeY * scale) / 2.0 - minY * scale;

        return (scale, offsetX, offsetY);
    }

    static Point ToScreen(Vec2 p, (double Scale, double OffsetX, double OffsetY) xf) =>
        new(p.X * xf.Scale + xf.OffsetX, p.Y * xf.Scale + xf.OffsetY);

    static void DrawPolyline(DrawingContext ctx, Pen pen, List<Vec2> pts,
        (double Scale, double OffsetX, double OffsetY) xf)
    {
        for (var i = 0; i < pts.Count - 1; i++)
            ctx.DrawLine(pen, ToScreen(pts[i], xf), ToScreen(pts[i + 1], xf));
    }

    static void DrawLabel(DrawingContext ctx, string text, Vec2 pos,
        (double Scale, double OffsetX, double OffsetY) xf, Typeface typeface, IBrush brush)
    {
        var pt = ToScreen(pos, xf);
        var formatted = new FormattedText(
            text,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            9,
            brush);
        ctx.DrawText(formatted, new Point(pt.X + 4, pt.Y - 10));
    }
}

record struct Vec2(double X, double Y);
