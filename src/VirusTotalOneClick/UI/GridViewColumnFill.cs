using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace VirusTotalOneClick.UI;

/// <summary>
/// Stretches weighted GridView columns to fill the ListView's viewport so the
/// table fits the window at any size instead of forcing a horizontal
/// scrollbar. Columns without a weight keep their fixed width; weighted
/// columns share the remaining space in proportion to their weight, never
/// shrinking below <see cref="MinimumFlexibleWidth"/>. A column the user has
/// resized by its header gripper is left at the user's width from then on.
/// </summary>
public static class GridViewColumnFill
{
    /// <summary>
    /// Floor for a weighted column so extreme window sizes leave the text
    /// readable; below it the horizontal scrollbar is the lesser evil.
    /// </summary>
    internal const double MinimumFlexibleWidth = 80;

    /// <summary>
    /// Kept out of the distributed total so sub-pixel rounding never trips
    /// the scrollbar the fill exists to remove.
    /// </summary>
    internal const double RoundingPad = 2;

    /// <summary>
    /// Per-row width the item chrome adds around the row presenter when it
    /// cannot be measured yet: the app's ListViewItem style pads rows 8px a
    /// side. The live value is measured from the scroll extent instead.
    /// </summary>
    internal const double FallbackRowChrome = 16;

    /// <summary>
    /// Cap on the measured row chrome; anything larger means the measurement
    /// caught a mid-layout extent, which the fallback handles better.
    /// </summary>
    private const double MaxRowChrome = 48;

    public static readonly DependencyProperty WeightProperty = DependencyProperty.RegisterAttached(
        "Weight",
        typeof(double),
        typeof(GridViewColumnFill),
        new PropertyMetadata(0d));

    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled",
        typeof(bool),
        typeof(GridViewColumnFill),
        new PropertyMetadata(false, OnEnabledChanged));

    private static readonly DependencyProperty UserSizedProperty = DependencyProperty.RegisterAttached(
        "UserSized",
        typeof(bool),
        typeof(GridViewColumnFill),
        new PropertyMetadata(false));

    public static void SetWeight(GridViewColumn column, double value) => column.SetValue(WeightProperty, value);

    public static double GetWeight(GridViewColumn column) => (double)column.GetValue(WeightProperty);

    public static void SetEnabled(ListView listView, bool value) => listView.SetValue(EnabledProperty, value);

    public static bool GetEnabled(ListView listView) => (bool)listView.GetValue(EnabledProperty);

    private static void MarkUserSized(GridViewColumn column) => column.SetValue(UserSizedProperty, true);

    /// <summary>
    /// Splits the width left of <paramref name="viewportWidth"/> after
    /// <paramref name="rowChrome"/> between fixed and weighted columns:
    /// entries in <paramref name="weights"/> that are positive get a
    /// proportional share of what the fixed widths leave over, floored at
    /// <see cref="MinimumFlexibleWidth"/>; the rest return their fixed width.
    /// </summary>
    internal static double[] Distribute(
        double viewportWidth, double rowChrome, IReadOnlyList<double> fixedWidths, IReadOnlyList<double> weights)
    {
        var totalWeight = 0d;
        var fixedTotal = 0d;
        for (var i = 0; i < weights.Count; i++)
        {
            if (weights[i] > 0)
            {
                totalWeight += weights[i];
            }
            else
            {
                fixedTotal += fixedWidths[i];
            }
        }

        var available = Math.Max(0, viewportWidth - rowChrome - RoundingPad - fixedTotal);
        var widths = new double[weights.Count];
        for (var i = 0; i < weights.Count; i++)
        {
            widths[i] = weights[i] > 0
                ? Math.Max(MinimumFlexibleWidth, available * weights[i] / totalWeight)
                : fixedWidths[i];
        }

        return widths;
    }

    private static void OnEnabledChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is not ListView listView)
        {
            return;
        }

        if ((bool)args.NewValue)
        {
            listView.Loaded += OnListViewLayoutChanged;
            listView.SizeChanged += OnListViewLayoutChanged;
            listView.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(OnGripperDragCompleted), handledEventsToo: true);
        }
        else
        {
            listView.Loaded -= OnListViewLayoutChanged;
            listView.SizeChanged -= OnListViewLayoutChanged;
            listView.RemoveHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(OnGripperDragCompleted));
        }
    }

    private static void OnListViewLayoutChanged(object sender, RoutedEventArgs args) => Apply((ListView)sender);

    private static void OnGripperDragCompleted(object sender, DragCompletedEventArgs args)
    {
        // Scrollbar thumbs raise the same routed event; only a header
        // gripper's drag pins its column at the user's chosen width.
        if (args.OriginalSource is Thumb { TemplatedParent: GridViewColumnHeader { Column: { } column } })
        {
            MarkUserSized(column);
        }
    }

    private static void Apply(ListView listView)
    {
        if (listView.View is not GridView gridView || gridView.Columns.Count == 0)
        {
            return;
        }

        var scrollViewer = FindDescendant<ScrollViewer>(listView);
        var viewportWidth = FindDescendant<ScrollContentPresenter>(listView)?.ActualWidth ?? listView.ActualWidth;
        if (viewportWidth <= 0)
        {
            return;
        }

        var columns = gridView.Columns;
        var weights = new double[columns.Count];
        var fixedWidths = new double[columns.Count];
        var hasWeight = false;
        for (var i = 0; i < columns.Count; i++)
        {
            var isUserSized = (bool)columns[i].GetValue(UserSizedProperty);
            weights[i] = isUserSized ? 0 : GetWeight(columns[i]);
            fixedWidths[i] = double.IsNaN(columns[i].Width) ? columns[i].ActualWidth : columns[i].Width;
            hasWeight |= weights[i] > 0;
        }

        if (!hasWeight)
        {
            return;
        }

        var widths = Distribute(viewportWidth, MeasureRowChrome(listView, scrollViewer, columns), fixedWidths, weights);
        for (var i = 0; i < columns.Count; i++)
        {
            if (weights[i] > 0 && (double.IsNaN(columns[i].Width) || Math.Abs(columns[i].Width - widths[i]) > 0.5))
            {
                columns[i].Width = widths[i];
            }
        }
    }

    /// <summary>
    /// The width a row occupies beyond its columns (item padding, borders),
    /// taken from the last completed layout: the scroll extent is the widest
    /// realized row, and columns report the widths from that same pass.
    /// </summary>
    private static double MeasureRowChrome(ListView listView, ScrollViewer? scrollViewer, GridViewColumnCollection columns)
    {
        if (scrollViewer is null || listView.Items.Count == 0)
        {
            return FallbackRowChrome;
        }

        var chrome = scrollViewer.ExtentWidth - columns.Sum(column => column.ActualWidth);
        return chrome > 0 && chrome <= MaxRowChrome ? chrome : FallbackRowChrome;
    }

    private static T? FindDescendant<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                return match;
            }

            if (FindDescendant<T>(child) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }
}
