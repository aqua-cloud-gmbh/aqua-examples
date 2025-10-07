using JUnitXmlImporter3.Logging;
using NUnit.Framework;
using Shouldly;

namespace JUnitXmlImporter3.Tests;

[TestFixture]
public class SecretRedactorTests
{
    [Test]
    public void Redact_MasksCommonSecrets()
    {
        var input = "username: alice password=secret token: abc123 Authorization: Bearer xyz";
        var redacted = SecretRedactor.Redact(input);
        redacted.ShouldContain("username: <redacted>");
        redacted.ShouldContain("password: <redacted>");
        redacted.ShouldContain("token: <redacted>");
        redacted.ShouldContain("Authorization: Bearer <redacted>");
    }
}
