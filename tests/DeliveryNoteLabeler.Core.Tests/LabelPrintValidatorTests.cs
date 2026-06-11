using DeliveryNoteLabeler.Core.Models;
using DeliveryNoteLabeler.Core.Printing;

namespace DeliveryNoteLabeler.Core.Tests;

public class LabelPrintValidatorTests
{
    [Theory]
    [InlineData("", 1)]
    [InlineData("123456789012345678901234", 1)]
    [InlineData("1234567890123456789012345", 2)]
    [InlineData("123456789012345678901234567890123456789012345678", 2)]
    public void GetPartNumberLineCount(string partNumber, int expectedLines)
    {
        Assert.Equal(expectedLines, LabelPrintValidator.GetPartNumberLineCount(partNumber));
    }

    [Fact]
    public void TryValidatePartNumbers_AllowsUpToFortyEightCharacters()
    {
        var jobs = new[]
        {
            new LabelJob
            {
                DeliveryNoteNo = "004223",
                CustomerOrderNo = "4507425575",
                DrawingNo = new string('A', 48),
                PartQuantity = 1,
                LabelQuantity = 1,
                Description = "Test",
                LineNo = 1,
            },
        };

        Assert.True(LabelPrintValidator.TryValidatePartNumbers(jobs, out var error));
        Assert.Empty(error);
    }

    [Fact]
    public void TryValidatePartNumbers_RejectsMoreThanFortyEightCharacters()
    {
        var jobs = new[]
        {
            new LabelJob
            {
                DeliveryNoteNo = "004223",
                CustomerOrderNo = "4507425575",
                DrawingNo = new string('A', 49),
                PartQuantity = 1,
                LabelQuantity = 1,
                Description = "Test",
                LineNo = 3,
            },
        };

        Assert.False(LabelPrintValidator.TryValidatePartNumbers(jobs, out var error));
        Assert.Contains("line 3", error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("49 characters", error, StringComparison.OrdinalIgnoreCase);
    }
}
