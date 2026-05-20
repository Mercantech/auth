namespace Auth.API.Services;

public enum LinkExternalOutcome
{
    /// <summary>Kobling oprettet eller allerede korrekt på denne bruger.</summary>
    Linked,

    /// <summary>Samme udbyder-identitet bruges allerede af en anden Mercantec-bruger.</summary>
    ConflictOtherUser,
}

public enum UnlinkExternalLoginResult
{
    Success,
    NotFound,
    CannotRemoveLastLoginMethod,
}
