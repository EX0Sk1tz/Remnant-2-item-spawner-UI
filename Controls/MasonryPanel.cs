using System;
using System.Linq;
using System.Windows;
using Size = System.Windows.Size;
using Panel = System.Windows.Controls.Panel;

namespace Remnant2UnlockerApp.Controls;

public sealed class MasonryPanel : Panel
{
    public static readonly DependencyProperty ColumnWidthProperty =
        DependencyProperty.Register(nameof(ColumnWidth), typeof(double), typeof(MasonryPanel),
            new FrameworkPropertyMetadata(300.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public double ColumnWidth
    {
        get => (double)GetValue(ColumnWidthProperty);
        set => SetValue(ColumnWidthProperty, value);
    }

    public static readonly DependencyProperty ColumnSpacingProperty =
        DependencyProperty.Register(nameof(ColumnSpacing), typeof(double), typeof(MasonryPanel),
            new FrameworkPropertyMetadata(12.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public double ColumnSpacing
    {
        get => (double)GetValue(ColumnSpacingProperty);
        set => SetValue(ColumnSpacingProperty, value);
    }

    public static readonly DependencyProperty RowSpacingProperty =
        DependencyProperty.Register(nameof(RowSpacing), typeof(double), typeof(MasonryPanel),
            new FrameworkPropertyMetadata(12.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public double RowSpacing
    {
        get => (double)GetValue(RowSpacingProperty);
        set => SetValue(RowSpacingProperty, value);
    }

    private int[] _columnAssignment = Array.Empty<int>();

    private int ResolveColumnCount(double availableWidth)
    {
        var columnWidth = ColumnWidth;
        if (double.IsInfinity(availableWidth) || availableWidth <= 0)
            return 1;

        return Math.Max(1, (int)Math.Floor((availableWidth + ColumnSpacing) / (columnWidth + ColumnSpacing)));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var columnWidth = ColumnWidth;
        var columns = ResolveColumnCount(availableSize.Width);
        var columnHeights = new double[columns];
        _columnAssignment = new int[InternalChildren.Count];

        for (var i = 0; i < InternalChildren.Count; i++)
        {
            var child = InternalChildren[i];
            child.Measure(new Size(columnWidth, double.PositiveInfinity));

            var column = 0;
            for (var c = 1; c < columns; c++)
            {
                if (columnHeights[c] < columnHeights[column])
                    column = c;
            }

            _columnAssignment[i] = column;
            columnHeights[column] += child.DesiredSize.Height + RowSpacing;
        }

        var totalHeight = columnHeights.Length == 0 ? 0 : columnHeights.Max();
        var totalWidth = columns * columnWidth + (columns - 1) * ColumnSpacing;
        return new Size(totalWidth, totalHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var columnWidth = ColumnWidth;
        var columns = ResolveColumnCount(finalSize.Width);
        var columnHeights = new double[columns];

        for (var i = 0; i < InternalChildren.Count; i++)
        {
            var child = InternalChildren[i];
            var column = i < _columnAssignment.Length ? _columnAssignment[i] % columns : 0;
            var x = column * (columnWidth + ColumnSpacing);
            var y = columnHeights[column];

            child.Arrange(new Rect(x, y, columnWidth, child.DesiredSize.Height));
            columnHeights[column] += child.DesiredSize.Height + RowSpacing;
        }

        return finalSize;
    }
}
