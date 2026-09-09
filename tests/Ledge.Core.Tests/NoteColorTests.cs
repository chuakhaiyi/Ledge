namespace Ledge.Core.Tests;

using FluentAssertions;
using Ledge.Core.Models;
using Xunit;

public class NoteColorTests
{
    [Theory]
    [InlineData(NoteColor.Butter, "#1B1D22")]
    [InlineData(NoteColor.Moss, "#1B1D22")]
    [InlineData(NoteColor.Ink, "#1B1D22")]
    public void Light_note_colors_use_dark_foreground(NoteColor color, string expected)
    {
        color.ToForegroundHex().Should().Be(expected);
    }
}
