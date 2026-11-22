// tests/ElectroMarketing.Domain.Tests/ShortTokenTests.cs
using ElectroMarketing.Domain.Campaigns;

namespace ElectroMarketing.Domain.Tests;

public class ShortTokenTests
{
    [Fact]
    public void Tokens_are_7_base62_characters_and_do_not_repeat()
    {
        var tokens = Enumerable.Range(0, 10_000).Select(_ => ShortToken.New()).ToList();

        tokens.ShouldAllBe(t => t.Length == 7 && t.All(char.IsAsciiLetterOrDigit));
        tokens.Distinct().Count().ShouldBe(10_000);
    }

    [Theory]
    [InlineData("aZ09xYq", true)]
    [InlineData("aZ09xY", false)]
    [InlineData("aZ09xY!", false)]
    [InlineData("graphq1", true)]
    public void Validates_shape(string token, bool valid) => ShortToken.IsValid(token).ShouldBe(valid);
}
