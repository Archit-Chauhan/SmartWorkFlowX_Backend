using System.Net.Mail;

namespace SmartWorkFlowX.Application.Services
{
    /// <summary>
    /// Pure validation rules for the Admin "create user" request (no database).
    /// The database-dependent rules (duplicate e-mail, role exists) stay in AdminService,
    /// which calls these two methods in the contract order around them.
    /// </summary>
    public static class UserCreateValidator
    {
        public const int MaxNameLength = 100;
        public const int MaxEmailLength = 200;
        public const int MinPasswordLength = 8;

        public const string NameRequired = "Name is required.";
        public const string NameTooLong = "Name must be 100 characters or fewer.";
        public const string EmailInvalid = "Enter a valid email address.";
        public const string EmailTooLong = "Email must be 200 characters or fewer.";
        public const string PasswordTooShort = "Password must be at least 8 characters.";

        /// <summary>
        /// Rules 1-4: name required, name length, e-mail valid, e-mail length.
        /// Returns the trimmed name and the trimmed, lower-cased e-mail.
        /// </summary>
        public static void ValidateIdentity(string? name, string? email, out string cleanName, out string cleanEmail)
        {
            string trimmedName = (name ?? string.Empty).Trim();
            string trimmedEmail = (email ?? string.Empty).Trim();

            if (trimmedName.Length == 0)
                throw new ArgumentException(NameRequired);
            if (trimmedName.Length > MaxNameLength)
                throw new ArgumentException(NameTooLong);
            if (!IsValidEmail(trimmedEmail))
                throw new ArgumentException(EmailInvalid);
            if (trimmedEmail.Length > MaxEmailLength)
                throw new ArgumentException(EmailTooLong);

            cleanName = trimmedName;
            cleanEmail = trimmedEmail.ToLowerInvariant();
        }

        /// <summary>Rule 7: password not empty and at least 8 characters (not trimmed).</summary>
        public static void ValidatePassword(string? password)
        {
            if (string.IsNullOrEmpty(password) || password.Length < MinPasswordLength)
                throw new ArgumentException(PasswordTooShort);
        }

        /// <summary>
        /// Conservative check: System.Net.Mail parses it and the parsed address equals the input
        /// exactly (so display names, comments and surrounding text are rejected), the domain has a dot,
        /// and there is no whitespace or comma.
        /// </summary>
        public static bool IsValidEmail(string email)
        {
            if (string.IsNullOrEmpty(email)) return false;
            if (email.IndexOfAny(new[] { ' ', '\t', '\r', '\n', ',', ';', '<', '>', '"' }) >= 0) return false;

            int at = email.LastIndexOf('@');
            if (at <= 0 || at == email.Length - 1) return false;
            if (email.IndexOf('@') != at) return false;

            string domain = email.Substring(at + 1);
            if (!domain.Contains('.') || domain.StartsWith(".") || domain.EndsWith(".")) return false;

            // Over-long input is rejected later by the length rule; do not depend on the parser for it.
            if (email.Length > MaxEmailLength) return true;

            try
            {
                var parsed = new MailAddress(email);
                return string.Equals(parsed.Address, email, StringComparison.OrdinalIgnoreCase);
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
