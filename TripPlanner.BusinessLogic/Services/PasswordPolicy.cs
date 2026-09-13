using TripPlanner.Core.Common;

namespace TripPlanner.BusinessLogic.Services;

/// <summary>
/// Password rules, in one place so the API, the emails and the client all agree.
///
/// <para>
/// Length is the rule that actually matters, so composition requirements (a digit,
/// a symbol…) are deliberately absent: they push people towards "Password1!" while
/// blocking genuinely strong passphrases. What is rejected instead is a password
/// that is obviously guessable — the top of every cracking dictionary — or that
/// simply repeats the user's own details.
/// </para>
/// </summary>
public static class PasswordPolicy
{
    /// <summary>
    /// The passwords tried first in any real attack. Not a full breach list — a
    /// proper check would query Have I Been Pwned's k-anonymity API — but it removes
    /// the handful that would otherwise be chosen over and over.
    /// </summary>
    private static readonly HashSet<string> CommonPasswords = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "password1", "password123", "passw0rd", "p@ssword", "p@ssw0rd",
        "12345678", "123456789", "1234567890", "qwertyui", "qwerty123", "1q2w3e4r",
        "iloveyou", "princess", "football", "baseball", "welcome1", "welcome123",
        "admin123", "administrator", "letmein1", "monkey123", "sunshine", "starwars",
        "trustno1", "dragon123", "master123", "shadow123", "superman", "batman123",
        "parola123", "parolamea", "qwertzui", "asdfghjk", "abcd1234", "test1234",
        "changeme", "secret123", "tripplanner", "tripplanner1",
    };

    public static Error? Validate(string? password, AuthSettings settings, string? email = null,
                                  string? firstName = null, string? lastName = null)
    {
        if (string.IsNullOrWhiteSpace(password))
            return ErrorCodes.PasswordTooWeak;

        if (password.Length < settings.PasswordMinLength || password.Length > settings.PasswordMaxLength)
            return ErrorCodes.PasswordTooWeak;

        if (CommonPasswords.Contains(password))
            return ErrorCodes.PasswordTooCommon;

        // A single repeated character reaches any length limit while carrying almost
        // no entropy.
        if (password.Distinct().Count() < 4)
            return ErrorCodes.PasswordTooWeak;

        if (ContainsPersonalDetail(password, email, firstName, lastName))
            return ErrorCodes.PasswordContainsPersonalInfo;

        return null;
    }

    /// <summary>
    /// Rejects passwords built from the user's own name or email, which is exactly
    /// what a targeted guess tries first.
    /// </summary>
    private static bool ContainsPersonalDetail(
        string password, string? email, string? firstName, string? lastName)
    {
        var lowered = password.ToLowerInvariant();

        var localPart = email?.Split('@')[0];

        foreach (var detail in new[] { localPart, firstName, lastName })
        {
            if (string.IsNullOrWhiteSpace(detail) || detail.Length < 4) continue;

            if (lowered.Contains(detail.ToLowerInvariant())) return true;
        }

        return false;
    }
}
