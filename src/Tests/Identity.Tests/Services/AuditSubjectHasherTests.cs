using Microsoft.Extensions.Options;
using Trislab.Modules.Identity.Authorization.Jwt;
using Trislab.Modules.Identity.Services;

namespace Identity.Tests.Services;

public sealed class AuditSubjectHasherTests
{
    private static AuditSubjectHasher Hasher(string key) =>
        new(Options.Create(new JwtOptions { SigningKey = key }));

    [Fact]
    public void The_Same_Address_Gets_The_Same_Subject_Whatever_Its_Case_And_Spaces()
    {
        var hasher = Hasher(new string('a', 64));

        hasher.Email(" User@Example.com ").ShouldBe(hasher.Email("user@example.com"));
    }

    [Fact]
    public void The_Subject_Never_Contains_The_Address()
    {
        var subject = Hasher(new string('a', 64)).Email("user@example.com");

        subject.ShouldStartWith("email:");
        subject.ShouldNotContain("user", Case.Insensitive);
        subject.Length.ShouldBe("email:".Length + 16);
    }

    [Fact]
    public void Another_Key_Gives_Another_Subject_So_A_List_Of_Addresses_Cannot_Reverse_It()
    {
        Hasher(new string('a', 64)).Email("user@example.com")
            .ShouldNotBe(Hasher(new string('b', 64)).Email("user@example.com"));
    }

    [Fact]
    public void A_Host_Without_A_Signing_Key_Fails_At_Once()
    {
        Should.Throw<InvalidOperationException>(() => Hasher(string.Empty));
    }
}
