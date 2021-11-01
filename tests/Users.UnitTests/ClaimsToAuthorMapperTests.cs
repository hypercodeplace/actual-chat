﻿using ActualChat.Users.Db;

namespace ActualChat.Users.UnitTests;

public class ClaimsToAuthorMapperTests
{
    [Fact]
    public async Task Populate_Should_Transform_Default_GitHubClaims()
    {
        DbDefaultAuthor author = new();
        var service = new ClaimsToAuthorMapper();
        await service.Populate(author, new Dictionary<string, string>() {
            {"http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name","vchirikov"},
            {"urn:github:name","Vladimir Chirikov"},
        }).ConfigureAwait(false);

        author.Name.Should().NotBeNullOrEmpty();
        author.Nickname.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Populate_Should_Transform_Default_MicrosoftClaims()
    {
        DbDefaultAuthor author = new();
        var service = new ClaimsToAuthorMapper();
        await service.Populate(author, new Dictionary<string, string>() {
            {"http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name","vchirikov"},
            {"http://schemas.xmlsoap.org/ws/2005/05/identity/claims/surname","Chirikov"},
            {"http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname","Vladimir"},
        }).ConfigureAwait(false);

        author.Name.Should().NotBeNullOrEmpty();
        author.Nickname.Should().NotBeNullOrEmpty();
    }
}
