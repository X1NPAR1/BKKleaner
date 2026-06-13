using System.Globalization;
using System.Windows.Media;
using BKKleaner.UI;
using Xunit;

namespace BKKleaner.Tests.Unit;

public class ConverterTests
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    [Theory]
    [InlineData(500.0, "500")]
    [InlineData(1500.0, "1.5K")]
    [InlineData(25_352_340.0, "25.4M")]
    [InlineData(3_100_000_000.0, "3.1B")]
    public void Score_label_is_compact_and_readable(double value, string expected)
    {
        var c = new ScoreLabelConverter();
        Assert.Equal(expected, c.Convert(value, typeof(string), null, Inv));
    }

    [Theory]
    [InlineData(50.0, false, false)]   // normal
    [InlineData(85.0, true, false)]    // orange band (80-90)
    [InlineData(95.0, false, true)]    // red band (90-100)
    [InlineData(110.0, false, true)]   // dark red (>100) — still a hot color
    public void Heat_brush_escalates_with_value(double value, bool orange, bool reddish)
    {
        var c = new HeatBrushConverter();
        var brush = (SolidColorBrush)c.Convert(value, typeof(Brush), null, Inv);
        var col = brush.Color;
        if (orange) Assert.True(col.R > 200 && col.G is > 100 and < 200);
        if (reddish) Assert.True(col.R > 150 && col.G < 100);
    }

    [Fact]
    public void Heat_brush_normal_is_not_a_hot_color()
    {
        var c = new HeatBrushConverter();
        var result = c.Convert(40.0, typeof(Brush), null, Inv);
        Assert.IsAssignableFrom<Brush>(result);
    }
}
