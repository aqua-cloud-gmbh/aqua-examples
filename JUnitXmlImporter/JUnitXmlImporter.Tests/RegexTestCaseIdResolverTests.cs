using System.Globalization;
using JUnitXmlImporter3.Options;
using JUnitXmlImporter3.Services;
using NUnit.Framework;
using Shouldly;

namespace JUnitXmlImporter3.Tests;

[TestFixture]
public class RegexTestCaseIdResolverTests
{
    [TestCase("TC123", 123)]
    [TestCase("TC-123", 123)]
    [TestCase("[TC:123]", 123)]
    [TestCase("MyTest TC 123", 123)]
    [TestCase("prefix tc-42 suffix", 42)] // case-insensitive
    public void ResolveId_ValidPatterns_ReturnsExpectedId(string input, int expected)
    {
        var resolver = new RegexTestCaseIdResolver(new MappingOptions());
        var id = resolver.ResolveId(input);
        id.ShouldBe(expected);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("no id here")] 
    [TestCase("TC0")]
    [TestCase("TC000")] // parses to 0 -> invalid
    [TestCase("TC99999999999")] // 11 digits -> no match per pattern
    public void ResolveId_InvalidPatterns_ReturnsNull(string? input)
    {
        var resolver = new RegexTestCaseIdResolver(new MappingOptions());
        var id = resolver.ResolveId(input ?? string.Empty);
        id.ShouldBeNull();
    }

    [Test]
    public void ResolveId_IsCultureInvariant()
    {
        var original = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("tr-TR");
            var resolver = new RegexTestCaseIdResolver(new MappingOptions());
            resolver.ResolveId("TC123").ShouldBe(123);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = original;
        }
    }
}
