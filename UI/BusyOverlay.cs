using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace BKKleaner.UI;

/// <summary>
/// A scrim + ring spinner shown over content while a long operation runs.
/// Set <see cref="IsActive"/> (bind it to a VM IsBusy flag) and optionally <see cref="Message"/>.
/// The spinner only animates when <see cref="AnimationSettings.Enabled"/> is true.
/// </summary>
public sealed class BusyOverlay : Control
{
    private const string SpinnerName = "PART_Spinner";
    private RotateTransform? _rotate;
    private Storyboard? _spin;

    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(
        nameof(IsActive), typeof(bool), typeof(BusyOverlay),
        new PropertyMetadata(false, OnIsActiveChanged));

    public static readonly DependencyProperty MessageProperty = DependencyProperty.Register(
        nameof(Message), typeof(string), typeof(BusyOverlay), new PropertyMetadata(string.Empty));

    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    static BusyOverlay() =>
        DefaultStyleKeyProperty.OverrideMetadata(typeof(BusyOverlay),
            new FrameworkPropertyMetadata(typeof(BusyOverlay)));

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (GetTemplateChild(SpinnerName) is Ellipse spinner)
        {
            _rotate = new RotateTransform();
            spinner.RenderTransform = _rotate;
            spinner.RenderTransformOrigin = new Point(0.5, 0.5);
        }
        UpdateState();
    }

    private static void OnIsActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((BusyOverlay)d).UpdateState();

    private void UpdateState()
    {
        Visibility = IsActive ? Visibility.Visible : Visibility.Collapsed;
        if (IsActive && AnimationSettings.Enabled) StartSpin();
        else StopSpin();
    }

    private void StartSpin()
    {
        if (_rotate is null || _spin is not null) return;
        var anim = new DoubleAnimation(0, 360, new Duration(TimeSpan.FromSeconds(1)))
        {
            RepeatBehavior = RepeatBehavior.Forever
        };
        _spin = new Storyboard();
        _spin.Children.Add(anim);
        Storyboard.SetTarget(anim, this);
        // Animate the transform directly.
        _rotate.BeginAnimation(RotateTransform.AngleProperty, anim);
    }

    private void StopSpin()
    {
        _rotate?.BeginAnimation(RotateTransform.AngleProperty, null);
        _spin = null;
    }
}
