using System;
using System.Numerics;
using Uno.Controls.Presentation;
using Xunit;

namespace TreeDataGrid.Uno.Tests;

public sealed class RowIndexMathTests
{
    [Theory]
    [InlineData(int.MaxValue - 2, 10, 5, int.MaxValue - 7)]
    [InlineData(int.MaxValue - 2, 100, 99, int.MaxValue - 3)]
    [InlineData(7, 5, 0, 2)]
    [InlineData(0, 0, 0, 0)]
    public void Suffix_remapping_checks_the_final_index(int index, int removed, int added, int expected) =>
        Assert.Equal(expected, RowIndexMath.ShiftSuffix(index, removed, added));

    [Fact]
    public void A_genuinely_overflowing_result_is_still_rejected() =>
        Assert.Throws<OverflowException>(() => RowIndexMath.ShiftSuffix(int.MaxValue, 0, 1));

    [Fact]
    public void Surviving_indices_match_an_arbitrary_precision_oracle()
    {
        var random = new Random(190327);
        for (var iteration = 0; iteration < 4096; ++iteration)
        {
            var count = random.Next(1, int.MaxValue);
            var index = random.Next(count);
            var removed = (int)random.NextInt64(0, (long)index + 1);
            var added = (int)random.NextInt64(0, (long)int.MaxValue - count + removed + 1);
            var expected = (int)((BigInteger)index - removed + added);
            Assert.Equal(expected, RowIndexMath.ShiftSuffix(index, removed, added));
        }
    }
}
