using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;

namespace Nomi;

public sealed partial class MainWindow
{
    private Vector3KeyFrameAnimation SpringTo(Vector3 from, Vector3 to, Spring spring)
    {
        var animation = Compositor.CreateVector3KeyFrameAnimation();
        var linear = Compositor.CreateLinearEasingFunction();
        for (var i = 0; i < spring.Curve.Length; i++)
            animation.InsertKeyFrame(i / (float)(spring.Curve.Length - 1), Vector3.Lerp(from, to, spring.Curve[i]), linear);
        animation.Duration = TimeSpan.FromMilliseconds(spring.DurationMs);
        animation.Target = "Scale";
        return animation;
    }

    private ScalarKeyFrameAnimation ScalarSpring(float from, float to, Spring spring, double speed = 1)
    {
        var animation = Compositor.CreateScalarKeyFrameAnimation();
        var linear = Compositor.CreateLinearEasingFunction();
        for (var i = 0; i < spring.Curve.Length; i++)
            animation.InsertKeyFrame(i / (float)(spring.Curve.Length - 1), from + (to - from) * spring.Curve[i], linear);
        animation.Duration = TimeSpan.FromMilliseconds(spring.DurationMs * speed);
        return animation;
    }

    private void Springy(UIElement element, float scale)
    {
        if (!motion) return;
        var current = Vector3.One;
        void Go(float target, Spring spring)
        {
            var next = new Vector3(target, target, 1);
            element.StartAnimation(SpringTo(current, next, spring));
            current = next;
        }
        if (element is FrameworkElement framework)
            framework.SizeChanged += (_, _) => element.CenterPoint = new Vector3((float)framework.ActualWidth / 2, (float)framework.ActualHeight / 2, 0);
        float Press() => element is FrameworkElement f && f.ActualWidth >= Tokens.PressLargeFrom ? Tokens.PressLarge : Tokens.PressSmall;
        element.PointerEntered += (_, _) => Go(scale, Tokens.SettleSpring);
        element.PointerExited += (_, _) => Go(1, Tokens.ReleaseSpring);
        element.PointerCanceled += (_, _) => Go(1, Tokens.ReleaseSpring);
        element.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler((_, _) => Go(Press(), Tokens.PressSpring)), true);
        element.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler((_, _) => Go(scale, Tokens.ReleaseSpring)), true);
    }

    private void Morph(FrameworkElement pop, FrameworkElement? origin)
    {
        if (!motion || origin is null) return;
        pop.UpdateLayout();
        var w = (float)pop.ActualWidth;
        var h = (float)pop.ActualHeight;
        if (w <= 0 || h <= 0) return;
        var at = origin.TransformToVisual(pop).TransformPoint(new Windows.Foundation.Point(0, 0));
        var ow = Math.Min((float)origin.ActualWidth, w);
        var oh = Math.Min((float)origin.ActualHeight, h);
        var left = Math.Clamp((float)at.X, 0, w - ow);
        var top = Math.Clamp((float)at.Y, 0, h - oh);
        var visual = ElementCompositionPreview.GetElementVisual(pop);
        var clip = Compositor.CreateInsetClip(left, top, w - left - ow, h - top - oh);
        visual.Clip = clip;
        var spring = Tokens.MorphSpring;
        clip.StartAnimation("LeftInset", ScalarSpring(left, 0, spring));
        clip.StartAnimation("TopInset", ScalarSpring(top, 0, spring));
        clip.StartAnimation("RightInset", ScalarSpring(w - left - ow, 0, spring));
        clip.StartAnimation("BottomInset", ScalarSpring(h - top - oh, 0, spring));
        var dx = (float)at.X - left;
        var dy = (float)at.Y - top;
        var shift = Compositor.CreateVector3KeyFrameAnimation();
        var linear = Compositor.CreateLinearEasingFunction();
        for (var i = 0; i < spring.Curve.Length; i++)
            shift.InsertKeyFrame(i / (float)(spring.Curve.Length - 1), new Vector3(dx, dy, 0) * (1 - spring.Curve[i]), linear);
        shift.Duration = TimeSpan.FromMilliseconds(spring.DurationMs);
        shift.Target = "Translation";
        pop.StartAnimation(shift);
        var fade = Compositor.CreateScalarKeyFrameAnimation();
        fade.InsertKeyFrame(0, 0.55f);
        fade.InsertKeyFrame(1, 1);
        fade.Duration = TimeSpan.FromMilliseconds(spring.DurationMs / 2);
        visual.StartAnimation("Opacity", fade);
    }

    private void AsideScrimTapped(object sender, TappedRoutedEventArgs e)
    {
        asideOpen = false;
        LayOut();
    }
}
