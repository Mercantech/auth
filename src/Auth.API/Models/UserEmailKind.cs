namespace Auth.API.Models;

/// <summary>
/// Kategori for en tilknyttet e-mail. Bruges ved OAuth (<c>emailKind</c> på challenge) og lokalt signup (altid <see cref="Personal"/>).
/// </summary>
public enum UserEmailKind
{
    /// <summary>Primær/personlig mail (JWT <c>email</c> foretrækkes fra denne).</summary>
    Personal = 0,

    /// <summary>Arbejdsmail. Ved ny bruger uden personlig kopieres til <see cref="User.Email"/> indtil personlig tilføjes.</summary>
    Work = 1,

    /// <summary>Skolemail — samme som arbejde mht. kopiering til primær mail.</summary>
    School = 2,
}
