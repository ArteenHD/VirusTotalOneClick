using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace VirusTotalOneClick.AppTests;

internal static class VisualTreeAssert
{
    public static T? FindVisualChild<T>(DependencyObject parent)
        where T : DependencyObject
        => FindVisualChildren<T>(parent).FirstOrDefault();

    public static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent)
        where T : DependencyObject
    {
        foreach (var logicalChild in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        {
            if (logicalChild is T typedLogicalChild)
            {
                yield return typedLogicalChild;
            }

            foreach (var nested in FindVisualChildren<T>(logicalChild))
            {
                yield return nested;
            }
        }

        if (parent is not Visual and not System.Windows.Media.Media3D.Visual3D)
        {
            yield break;
        }

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild)
            {
                yield return typedChild;
            }

            foreach (var nested in FindVisualChildren<T>(child))
            {
                yield return nested;
            }
        }
    }

    public static void AssertWindowContainsText(DependencyObject window, string expectedText)
    {
        Assert.Contains(
            FindVisualChildren<TextBlock>(window),
            textBlock => textBlock.Visibility == Visibility.Visible
                && textBlock.Text.Contains(expectedText, StringComparison.OrdinalIgnoreCase));
    }

    public static void AssertWindowDoesNotContainText(DependencyObject window, string unexpectedText)
    {
        Assert.DoesNotContain(
            FindVisualChildren<TextBlock>(window),
            textBlock => textBlock.Visibility == Visibility.Visible
                && textBlock.Text.Contains(unexpectedText, StringComparison.OrdinalIgnoreCase));
    }
}
