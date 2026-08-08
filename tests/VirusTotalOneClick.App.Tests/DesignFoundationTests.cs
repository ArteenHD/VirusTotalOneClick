using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VirusTotalOneClick.UI;
using Wpf.Ui.Controls;

namespace VirusTotalOneClick.AppTests;

public class DesignFoundationTests
{
    [Theory]
    [InlineData("SystemFillColorSuccessBrush")]
    [InlineData("SystemFillColorCautionBrush")]
    [InlineData("SystemFillColorCriticalBrush")]
    [InlineData("TextFillColorPrimaryBrush")]
    [InlineData("TextFillColorSecondaryBrush")]
    [InlineData("SubtleFillColorSecondaryBrush")]
    [InlineData("SubtleFillColorTertiaryBrush")]
    [InlineData("AccentFillColorDefaultBrush")]
    [InlineData("AccentTextFillColorPrimaryBrush")]
    [InlineData("AccentTextFillColorSecondaryBrush")]
    public void TheStatusColorVocabularyResolvesFromTheme(string key)
    {
        StaTestRunner.Run(() =>
        {
            var brush = Application.Current.TryFindResource(key);

            Assert.NotNull(brush);
            Assert.IsAssignableFrom<Brush>(brush);
        });
    }

    [Theory]
    [InlineData("PagePadding")]
    [InlineData("SectionSpacing")]
    [InlineData("ControlSpacing")]
    [InlineData("LabelSpacing")]
    [InlineData("ControlCornerRadius")]
    [InlineData("CardCornerRadius")]
    [InlineData("WindowTitleTextStyle")]
    [InlineData("SectionHeaderTextStyle")]
    [InlineData("BodyTextStyle")]
    [InlineData("CaptionTextStyle")]
    public void TheDesignTokensResolveFromAppResources(string key)
    {
        StaTestRunner.Run(() =>
        {
            Assert.NotNull(Application.Current.TryFindResource(key));
        });
    }

    [Fact]
    public void AppWindowUsesFluentChromeDefaults()
    {
        StaTestRunner.Run(() =>
        {
            var window = new AppWindow();

            Assert.Equal(WindowBackdropType.Mica, window.WindowBackdropType);
            Assert.True(window.ExtendsContentIntoTitleBar);
            Assert.Equal(WindowStartupLocation.CenterScreen, window.WindowStartupLocation);
            Assert.Equal("VirusTotal OneClick", window.Title);

            window.Close();
        });
    }

    [Fact]
    public void AStyleDerivedWithBasedOnKeepsTheFluentTemplate()
    {
        StaTestRunner.Run(() =>
        {
            var fluentStyle = Application.Current.TryFindResource(typeof(CheckBox)) as Style;
            Assert.NotNull(fluentStyle);

            var derived = new Style(typeof(CheckBox), fluentStyle);
            derived.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(4)));

            var checkBox = new CheckBox { Style = derived, Content = "derived" };
            RenderInWindow(checkBox);

            Assert.Equal(new Thickness(4), checkBox.Margin);
            Assert.Same(FluentTemplateOf(fluentStyle), checkBox.Template);
        });
    }

    [Fact]
    public void AStyleWithoutBasedOnReplacesTheFluentTemplate()
    {
        StaTestRunner.Run(() =>
        {
            var fluentStyle = Application.Current.TryFindResource(typeof(CheckBox)) as Style;
            Assert.NotNull(fluentStyle);

            var bare = new Style(typeof(CheckBox));
            bare.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(4)));

            var checkBox = new CheckBox { Style = bare, Content = "bare" };
            RenderInWindow(checkBox);

            // Losing BasedOn silently reverts the control to the framework
            // template — this pair of tests exists to keep that mistake loud.
            Assert.NotSame(FluentTemplateOf(fluentStyle), checkBox.Template);
        });
    }

    [Fact]
    public void TheCheckBoxOverrideAlignsGlyphsAndKeepsTheFluentTemplate()
    {
        StaTestRunner.Run(() =>
        {
            var implicitStyle = Application.Current.TryFindResource(typeof(CheckBox)) as Style;
            Assert.NotNull(implicitStyle);

            var checkBox = new CheckBox { Content = "aligned" };
            var radioButton = new RadioButton { Content = "aligned" };
            RenderInWindow(checkBox);
            RenderInWindow(radioButton);

            Assert.Equal(new Thickness(0, 5, 11, 6), checkBox.Padding);
            Assert.Equal(radioButton.Padding.Left, checkBox.Padding.Left);
            Assert.Same(FluentTemplateOf(implicitStyle), checkBox.Template);
        });
    }

    private static System.Windows.Controls.ControlTemplate FluentTemplateOf(Style style)
    {
        for (var current = style; current is not null; current = current.BasedOn)
        {
            var setter = current.Setters.OfType<Setter>()
                .FirstOrDefault(s => s.Property == Control.TemplateProperty);
            if (setter?.Value is System.Windows.Controls.ControlTemplate template)
            {
                return template;
            }
        }

        throw new InvalidOperationException("The fluent style defines no template setter.");
    }

    private static void RenderInWindow(UIElement element)
    {
        var window = new Window
        {
            Content = element,
            ShowInTaskbar = false,
            ShowActivated = false,
            WindowStyle = WindowStyle.None,
            Width = 200,
            Height = 100,
        };
        try
        {
            window.Show();
            window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
        }
        finally
        {
            window.Close();
        }
    }
}
