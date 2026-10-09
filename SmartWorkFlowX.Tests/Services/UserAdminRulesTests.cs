using SmartWorkFlowX.Application.Services;
using SmartWorkFlowX.Domain.Repositories;

namespace SmartWorkFlowX.Tests.Services
{
    /// <summary>Pure tests: Manage Users list query parsing and the create-user validation rules.</summary>
    public class UserAdminRulesTests
    {
        // ── List query parser ─────────────────────────────────────────────────────

        [Fact(DisplayName = "TC-UQ01: List query defaults — page 1, limit 10, status all, sort name asc, no filters")]
        public void Parse_Defaults()
        {
            var q = UserListQueryParser.Parse(1, 10, null, null, null, null, null);

            Assert.Equal(1, q.Page);
            Assert.Equal(10, q.Limit);
            Assert.Null(q.Search);
            Assert.Null(q.RoleId);
            Assert.Equal("all", q.Status);
            Assert.Equal("name", q.Sort);
            Assert.False(q.Descending);
        }

        [Fact(DisplayName = "TC-UQ02: List query — blank values fall back to defaults, search is trimmed")]
        public void Parse_BlankValues_AreDefaults()
        {
            var q = UserListQueryParser.Parse(1, 10, "   ", " ", null, "", " ");

            Assert.Null(q.Search);
            Assert.Equal("all", q.Status);
            Assert.Equal("name", q.Sort);
            Assert.False(q.Descending);

            var q2 = UserListQueryParser.Parse(1, 10, "  ann ", null, null, null, null);
            Assert.Equal("ann", q2.Search);
        }

        [Fact(DisplayName = "TC-UQ03: List query — every documented status, sort and dir value is accepted")]
        public void Parse_AcceptsAllDocumentedValues()
        {
            foreach (var status in new[] { "all", "active", "deactivated" })
                Assert.Equal(status, UserListQueryParser.Parse(1, 10, null, status, null, null, null).Status);

            foreach (var sort in new[] { "name", "role", "status", "open", "added" })
                Assert.Equal(sort, UserListQueryParser.Parse(1, 10, null, null, null, sort, null).Sort);

            Assert.True(UserListQueryParser.Parse(1, 10, null, null, 4, null, "desc").Descending);
            Assert.False(UserListQueryParser.Parse(1, 10, null, null, null, null, "asc").Descending);
            Assert.Equal(4, UserListQueryParser.Parse(1, 10, null, null, 4, null, null).RoleId);
        }

        [Fact(DisplayName = "TC-UQ04: List query — unknown status is rejected with the contract message")]
        public void Parse_UnknownStatus_Throws()
        {
            var ex = Assert.Throws<ArgumentException>(() => UserListQueryParser.Parse(1, 10, null, "gone", null, null, null));
            Assert.Equal("Status must be all, active or deactivated.", ex.Message);
        }

        [Fact(DisplayName = "TC-UQ05: List query — unknown sort is rejected with the contract message")]
        public void Parse_UnknownSort_Throws()
        {
            var ex = Assert.Throws<ArgumentException>(() => UserListQueryParser.Parse(1, 10, null, null, null, "email", null));
            Assert.Equal("Sort must be one of: name, role, status, open, added.", ex.Message);
        }

        [Fact(DisplayName = "TC-UQ06: List query — unknown direction is rejected with the contract message")]
        public void Parse_UnknownDir_Throws()
        {
            var ex = Assert.Throws<ArgumentException>(() => UserListQueryParser.Parse(1, 10, null, null, null, null, "up"));
            Assert.Equal("Direction must be asc or desc.", ex.Message);
        }

        [Fact(DisplayName = "TC-UQ07: List query — values are case-sensitive (ASC is unknown)")]
        public void Parse_IsCaseSensitive()
        {
            Assert.Throws<ArgumentException>(() => UserListQueryParser.Parse(1, 10, null, "Active", null, null, null));
            Assert.Throws<ArgumentException>(() => UserListQueryParser.Parse(1, 10, null, null, null, "Name", null));
            Assert.Throws<ArgumentException>(() => UserListQueryParser.Parse(1, 10, null, null, null, null, "ASC"));
        }

        [Fact(DisplayName = "TC-UQ08: List query — when several values are bad, status is reported first, then sort, then dir")]
        public void Parse_ErrorOrder()
        {
            var ex = Assert.Throws<ArgumentException>(() => UserListQueryParser.Parse(1, 10, null, "x", null, "x", "x"));
            Assert.Equal("Status must be all, active or deactivated.", ex.Message);

            var ex2 = Assert.Throws<ArgumentException>(() => UserListQueryParser.Parse(1, 10, null, null, null, "x", "x"));
            Assert.Equal("Sort must be one of: name, role, status, open, added.", ex2.Message);
        }

        [Fact(DisplayName = "TC-UQ09: List query — page below 1 becomes 1, limit below 1 becomes the default 10")]
        public void Parse_PagingIsClamped()
        {
            var q = UserListQueryParser.Parse(-3, 0, null, null, null, null, null);
            Assert.Equal(1, q.Page);
            Assert.Equal(10, q.Limit);
        }

        // ── Create-user validator ─────────────────────────────────────────────────

        private static string IdentityError(string? name, string? email)
        {
            var ex = Assert.Throws<ArgumentException>(() =>
            {
                string n;
                string e;
                UserCreateValidator.ValidateIdentity(name, email, out n, out e);
            });
            return ex.Message;
        }

        [Fact(DisplayName = "TC-UC01: Name empty or whitespace — 'Name is required.'")]
        public void Name_Required()
        {
            Assert.Equal("Name is required.", IdentityError(null, "a@b.com"));
            Assert.Equal("Name is required.", IdentityError("", "a@b.com"));
            Assert.Equal("Name is required.", IdentityError("    ", "a@b.com"));
        }

        [Fact(DisplayName = "TC-UC02: Name over 100 characters (after trim) — 'Name must be 100 characters or fewer.'")]
        public void Name_TooLong()
        {
            Assert.Equal("Name must be 100 characters or fewer.", IdentityError(new string('a', 101), "a@b.com"));

            string n;
            string e;
            UserCreateValidator.ValidateIdentity("  " + new string('a', 100) + "  ", "a@b.com", out n, out e);
            Assert.Equal(100, n.Length);
        }

        [Fact(DisplayName = "TC-UC03: E-mail empty or not a valid address — 'Enter a valid email address.'")]
        public void Email_Invalid()
        {
            string[] bad =
            {
                null!, "", "   ", "plain", "a@", "@b.com", "a@b", "a b@c.com", "a@b..",
                "Jane <jane@example.com>", "a@b.com, c@d.com", "a@@b.com", "a@.com", "a@b.com."
            };

            foreach (var email in bad)
                Assert.Equal("Enter a valid email address.", IdentityError("Jane", email));
        }

        [Fact(DisplayName = "TC-UC04: E-mail over 200 characters — 'Email must be 200 characters or fewer.'")]
        public void Email_TooLong()
        {
            string longEmail = new string('a', 195) + "@x.com"; // 201 characters
            Assert.Equal(201, longEmail.Length);
            Assert.Equal("Email must be 200 characters or fewer.", IdentityError("Jane", longEmail));
        }

        [Fact(DisplayName = "TC-UC05: Rule order — name rules first, then e-mail validity, then e-mail length")]
        public void Identity_RuleOrder()
        {
            Assert.Equal("Name is required.", IdentityError("", "not-an-email"));
            Assert.Equal("Name must be 100 characters or fewer.", IdentityError(new string('n', 101), "not-an-email"));
            Assert.Equal("Enter a valid email address.", IdentityError("Jane", "not-an-email"));
        }

        [Fact(DisplayName = "TC-UC06: Valid identity — name trimmed, e-mail trimmed and lower-cased")]
        public void Identity_Normalised()
        {
            string n;
            string e;
            UserCreateValidator.ValidateIdentity("  Jane Doe ", "  Jane.Doe+Tag@Example.COM  ", out n, out e);

            Assert.Equal("Jane Doe", n);
            Assert.Equal("jane.doe+tag@example.com", e);
        }

        [Fact(DisplayName = "TC-UC07: Password empty or shorter than 8 — 'Password must be at least 8 characters.'")]
        public void Password_TooShort()
        {
            foreach (var pw in new string?[] { null, "", "1234567" })
            {
                var ex = Assert.Throws<ArgumentException>(() => UserCreateValidator.ValidatePassword(pw));
                Assert.Equal("Password must be at least 8 characters.", ex.Message);
            }
        }

        [Fact(DisplayName = "TC-UC08: Password of 8 or more characters is accepted (spaces count, nothing is trimmed)")]
        public void Password_Ok()
        {
            UserCreateValidator.ValidatePassword("12345678");
            UserCreateValidator.ValidatePassword("        ");
        }
    }
}
