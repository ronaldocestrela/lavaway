using CarWashSaaS.Shared.Contracts;
using Xunit;

namespace CarWashSaaS.UnitTests.Security;

public sealed class PiiRegexMaskerTests
{
    [Theory]
    [InlineData("123.456.789-00", "***.***.789-**")]
    [InlineData("12345678900", "***.***.789-**")]
    [InlineData("000.000.000-00", "***.***.000-**")]
    public void MaskCpf_ShouldMaskCpfCorrectly(string input, string expected)
    {
        var result = PiiRegexMasker.MaskCpf(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void MaskCpf_WhenNullOrEmpty_ShouldReturnEmpty()
    {
        Assert.Equal(string.Empty, PiiRegexMasker.MaskCpf(null));
        Assert.Equal(string.Empty, PiiRegexMasker.MaskCpf(""));
        Assert.Equal(string.Empty, PiiRegexMasker.MaskCpf("   "));
    }

    [Theory]
    [InlineData("11987654321", "(11) *****-4321")]
    [InlineData("1133334444", "(11) ****-4444")]
    [InlineData("+5511987654321", "+55 (11) *****-4321")]
    [InlineData("5511987654321", "+55 (11) *****-4321")]
    public void MaskPhone_ShouldMaskPhoneCorrectly(string input, string expected)
    {
        var result = PiiRegexMasker.MaskPhone(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("ronaldo@lavaway.com.br", "r***@lavaway.com.br")]
    [InlineData("admin@lavaway.com", "a***@lavaway.com")]
    [InlineData("contato@sub.dominio.org", "c***@sub.dominio.org")]
    public void MaskEmail_ShouldMaskEmailCorrectly(string input, string expected)
    {
        var result = PiiRegexMasker.MaskEmail(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("sk_live_1234567890abcdef", "sk_live_••••••••cdef")]
    [InlineData("sk_test_1234567890abcdef", "sk_test_••••••••cdef")]
    [InlineData("ev_apiKey_secret12345678", "ev_••••••••5678")]
    public void MaskSecretKey_ShouldMaskApiKeys(string input, string expected)
    {
        var result = PiiRegexMasker.MaskSecretKey(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void MaskAllPiiInText_ShouldMaskCpfEmailPhoneAndSecretsInLogMessage()
    {
        var text = "O cliente com CPF 123.456.789-00 e email ronaldo@lavaway.com.br ligou do 11987654321 usando chave sk_live_1234567890abcdef";

        var sanitized = PiiRegexMasker.MaskAllPiiInText(text);

        Assert.DoesNotContain("123.456.789-00", sanitized);
        Assert.DoesNotContain("ronaldo@lavaway.com.br", sanitized);
        Assert.DoesNotContain("11987654321", sanitized);
        Assert.DoesNotContain("1234567890abcdef", sanitized);

        Assert.Contains("***.***.789-**", sanitized);
        Assert.Contains("r***@lavaway.com.br", sanitized);
        Assert.Contains("(11) *****-4321", sanitized);
        Assert.Contains("sk_live_••••••••cdef", sanitized);
    }
}
