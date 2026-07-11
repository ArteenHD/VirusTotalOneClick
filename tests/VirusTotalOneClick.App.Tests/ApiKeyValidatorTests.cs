using VirusTotalOneClick.UI;

namespace VirusTotalOneClick.AppTests;

public class ApiKeyValidatorTests
{
    [Fact]
    public void AValid64CharHexKeyPassesFormatValidation()
    {
        Assert.True(ApiKeyValidator.IsValidFormat(new string('a', 32) + new string('F', 32)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc123")]
    [InlineData("testdev")]
    public void ShortOrNonHexKeysFailFormatValidation(string key)
    {
        Assert.False(ApiKeyValidator.IsValidFormat(key));
    }

    [Fact]
    public void KeysOfWrongLengthOrWithNonHexCharactersFail()
    {
        Assert.False(ApiKeyValidator.IsValidFormat(new string('a', 63)));
        Assert.False(ApiKeyValidator.IsValidFormat(new string('a', 65)));
        Assert.False(ApiKeyValidator.IsValidFormat(new string('g', 64)));
    }

    [Fact]
    public void KeysWithEmbeddedOrTrailingNewlinesFail()
    {
        Assert.False(ApiKeyValidator.IsValidFormat(new string('a', 64) + "\n"));
        Assert.False(ApiKeyValidator.IsValidFormat(new string('a', 64) + "\r\n"));
        Assert.False(ApiKeyValidator.IsValidFormat("\n" + new string('a', 64)));
    }

    [Fact]
    public void TheTestKeyIsHonoredOnlyWhenThePolicyAllowsIt()
    {
        Assert.True(new ApiKeyValidator(allowTestKey: true).IsTestDevKey("testdev"));
        Assert.False(new ApiKeyValidator(allowTestKey: false).IsTestDevKey("testdev"));
    }

    [Theory]
    [InlineData("TESTDEV")]
    [InlineData("testdev ")]
    [InlineData("")]
    [InlineData(null)]
    public void OnlyTheExactTestKeyMatchesEvenWhenAllowed(string? key)
    {
        Assert.False(new ApiKeyValidator(allowTestKey: true).IsTestDevKey(key));
    }
}
