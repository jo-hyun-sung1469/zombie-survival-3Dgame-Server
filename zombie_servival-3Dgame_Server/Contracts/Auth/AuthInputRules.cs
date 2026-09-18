namespace zombie_survival_3Dgame_Server.Contracts.Auth;

public static class AuthInputRules
{
    public const int UserNameMinLength = 3;
    public const int UserNameMaxLength = 30;
    public const string UserNamePattern = @"\A[a-z0-9가-힣ㄱ-ㅎㅏ-ㅣ]+\z";
    public const string UserNameError = "Username may contain only lowercase English letters, digits, and Korean letters.";

    public const int PasswordMinLength = 6;
    public const int PasswordMaxLength = 100;
    public const string PasswordPattern = @"\A[A-Za-z0-9!@#$%^*+=?/\-]+\z";
    public const string PasswordError = "Password may contain only English letters, digits, and !@#$%^*-=+?/ characters.";
}
