using System.Text.RegularExpressions;

public static class NicknameValidation
{
    public static bool IsValidNickname(string nickname, out string error)
    {
        error = "";

        if (string.IsNullOrWhiteSpace(nickname))
        {
            error = "Nickname cannot be empty.";
            return false;
        }

        nickname = nickname.Trim();

        if (nickname.Length < 3)
        {
            error = "Nickname must be at least 3 characters.";
            return false;
        }

        if (nickname.Length > 16)
        {
            error = "Nickname must be at most 16 characters.";
            return false;
        }

        if (!Regex.IsMatch(nickname, "^[a-zA-Z0-9]+$"))
        {
            error = "Nickname can only contain letters and numbers.";
            return false;
        }

        return true;
    }

    public static string FormatNickname(string nickname)
    {
        if (string.IsNullOrWhiteSpace(nickname))
            return "";

        return nickname.Trim().ToLowerInvariant();
    }
}