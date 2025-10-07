using JUnitXmlImporter3.Services;
using NUnit.Framework;
using Shouldly;

namespace JUnitXmlImporter3.Tests;

[TestFixture]
public class StrictIdResolversTests
{
    [TestCase("TS000000 scenario name", 0)]
    [TestCase("[TS123456] Something", 123456)]
    [TestCase("prefixTS654321suffix", 654321)]
    [TestCase("Class.Method TS000001 TC000002", 1)]
    [TestCase("TS1234567 in name", 1234567)]
    [TestCase("edge TS0000000", 0)]
    public void RegexScenarioIdResolver_Valid_ReturnsExpected(string input, int expected)
    {
        var resolver = new RegexScenarioIdResolver();
        resolver.ResolveId(input).ShouldBe(expected);
    }

    [TestCase("")]
    [TestCase("NoTSHere")]
    [TestCase("TS12345")] // five digits
    [TestCase("TS12345678")] // eight digits should be invalid
    public void RegexScenarioIdResolver_Invalid_ReturnsNull(string input)
    {
        var resolver = new RegexScenarioIdResolver();
        resolver.ResolveId(input).ShouldBeNull();
    }

    [TestCase("TC000000", 0)]
    [TestCase("[TC123456]", 123456)]
    [TestCase("xTC654321y", 654321)]
    [TestCase("Param name TC000042", 42)]
    public void RegexStrictTestCaseIdResolver_Valid_ReturnsExpected(string input, int expected)
    {
        var resolver = new RegexStrictTestCaseIdResolver();
        resolver.ResolveId(input).ShouldBe(expected);
    }

    [TestCase("TC-42")] // hyphen is not allowed in the strict pattern
    [TestCase("TC12345")] // five digits
    [TestCase("TC1234567")] // seven digits
    [TestCase("no id")] 
    public void RegexStrictTestCaseIdResolver_Invalid_ReturnsNull(string input)
    {
        var resolver = new RegexStrictTestCaseIdResolver();
        resolver.ResolveId(input).ShouldBeNull();
    }
}
