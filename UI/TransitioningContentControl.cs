using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace BKKleaner.UI;

/// <summary>
/// A ContentControl that fades and slides its new content in whenever it changes.
/// Honors <see cref="AnimationSettings.Enabled"/> — when off, content swaps instantly.
/// </summary>
public sealed class TransitioningContentControl : ContentControl
{
    protected override void OnContentChanged(object oldContent, object newContent)
    {
        base.OnContentChanged(oldContent, newContent);

        if (!AnimationSettings.Enabled || newContent is null)
        {
            ClearTransform();
            return;
        }

        var translate = new TranslateTransform(0, 14);
        RenderTransform = translate;
        Opacity = 0;

        var fade = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(260)))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        var slide = new DoubleAnimation(14, 0, new Duration(TimeSpan.FromMilliseconds(300)))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        slide.Completed += (_, _) => ClearTransform();

        BeginAnimation(OpacityProperty, fade);
        translate.BeginAnimation(TranslateTransform.YProperty, slide);
    }

    private void ClearTransform()
    {
        BeginAnimation(OpacityProperty, null);
        Opacity = 1;
        RenderTransform = Transform.Identity;
    }
}
