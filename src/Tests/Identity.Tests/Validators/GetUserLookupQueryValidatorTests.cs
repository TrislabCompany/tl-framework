using Trislab.Modules.Identity.Contracts.v1.Users.GetUserLookup;
using Trislab.Modules.Identity.Features.v1.Users.GetUserLookup;
using Trislab.Modules.Identity.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace Identity.Tests.Validators;

/// <summary>The user picker's lookup: exactly one of a search and ids, within their limits.</summary>
public sealed class GetUserLookupQueryValidatorTests
{
    private readonly GetUserLookupQueryValidator _sut = new(CreateLocalizer());

    private static IStringLocalizer<IdentityResources> CreateLocalizer()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLocalization(o => o.ResourcesPath = "");
        return services.BuildServiceProvider().GetRequiredService<IStringLocalizer<IdentityResources>>();
    }

    [Fact]
    public void Should_Pass_With_A_Search()
    {
        _sut.Validate(new GetUserLookupQuery { Search = "an" }).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Should_Pass_With_Ids()
    {
        _sut.Validate(new GetUserLookupQuery { Ids = ["a", "b"] }).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void Should_Fail_Without_A_Search_Or_Ids(string? search)
    {
        _sut.Validate(new GetUserLookupQuery { Search = search }).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Should_Fail_With_Both_A_Search_And_Ids()
    {
        _sut.Validate(new GetUserLookupQuery { Search = "an", Ids = ["a"] }).IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData("a")]
    [InlineData(" a ")]
    public void Should_Fail_When_The_Search_Is_Shorter_Than_Two_Characters(string search)
    {
        _sut.Validate(new GetUserLookupQuery { Search = search }).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Should_Fail_When_The_Search_Is_Too_Long()
    {
        _sut.Validate(new GetUserLookupQuery { Search = new string('a', 201) }).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Should_Fail_With_More_Than_The_Maximum_Ids()
    {
        var ids = Enumerable.Range(0, GetUserLookupQuery.MaxIds + 1).Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray();

        _sut.Validate(new GetUserLookupQuery { Ids = ids }).IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData(" ")]
    [InlineData(null)]
    public void Should_Fail_With_A_Blank_Id(string? id)
    {
        _sut.Validate(new GetUserLookupQuery { Ids = ["a", id!] }).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Should_Fail_With_An_Id_Over_450_Characters()
    {
        _sut.Validate(new GetUserLookupQuery { Ids = [new string('a', 451)] }).IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(GetUserLookupQuery.MaxResults, true)]
    [InlineData(GetUserLookupQuery.MaxResults + 1, false)]
    public void Should_Check_The_Limit(int limit, bool valid)
    {
        _sut.Validate(new GetUserLookupQuery { Search = "an", Limit = limit }).IsValid.ShouldBe(valid);
    }
}
