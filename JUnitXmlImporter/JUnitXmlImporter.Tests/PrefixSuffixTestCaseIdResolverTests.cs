using JUnitXmlImporter3.Options;
using JUnitXmlImporter3.Services;
using NUnit.Framework;
using Shouldly;

namespace JUnitXmlImporter3.Tests;

[TestFixture]
public class PrefixSuffixTestCaseIdResolverTests
{
    [TestCase("TC123", 123)]
    [TestCase("tc-987", 987)]
    [TestCase("TC:42", 42)]
    [TestCase("prefix TC 5 suffix", 5)]
    public void ResolveId_DefaultPrefix_NoDigitsLength_Works(string input, int expected)
    {
        var resolver = new PrefixSuffixTestCaseIdResolver(new MappingOptions());
        resolver.ResolveId(input).ShouldBe(expected);
    }

    [Test]
    public void ResolveId_CustomPrefix_WithExactDigitsLength_Valid()
    {
        var options = new MappingOptions { Prefix = "CASE", DigitsLength = 5 };
        var resolver = new PrefixSuffixTestCaseIdResolver(options);
        resolver.ResolveId("start CASE-01234 end").ShouldBe(1234);
        resolver.ResolveId("CASE 99999").ShouldBe(99999);
        resolver.ResolveId("case:00001").ShouldBe(1); // case-insensitive, leading zeros allowed
    }

    [Test]
    public void ResolveId_CustomPrefix_WithExactDigitsLength_InvalidWhenWrongLength()
    {
        var options = new MappingOptions { Prefix = "CASE", DigitsLength = 5 };
        var resolver = new PrefixSuffixTestCaseIdResolver(options);
        resolver.ResolveId("CASE 1234").ShouldBeNull(); // 4 digits
        resolver.ResolveId("CASE 123456").ShouldBeNull(); // 6 digits
    }

    [Test]
    public void ResolveId_Invalids_ReturnNull()
    {
        var resolver = new PrefixSuffixTestCaseIdResolver(new MappingOptions());
        resolver.ResolveId("").ShouldBeNull();
        resolver.ResolveId("no match here").ShouldBeNull();
        resolver.ResolveId("TC 0").ShouldBeNull(); // zero is not allowed
        resolver.ResolveId("TC : - ").ShouldBeNull(); // no digits
        resolver.ResolveId("TC 123456789012").ShouldBeNull(); // > 10 digits
    }
}
