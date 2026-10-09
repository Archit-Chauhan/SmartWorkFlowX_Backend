using SmartWorkFlowX.Application.Dtos;
using SmartWorkFlowX.Application.Services;

namespace SmartWorkFlowX.Tests.Services
{
    /// <summary>Pure tests for the workflow create/update validation table (no mocks, no database).</summary>
    public class WorkflowRequestValidatorTests
    {
        private static readonly HashSet<int> Roles = new HashSet<int> { 1, 2, 3 };

        private static WorkflowStepCreateDto Good(string name = "Review") =>
            new WorkflowStepCreateDto(1, 2, name, null, "GoBack", null);

        private static List<WorkflowStepCreateDto?> Steps(params WorkflowStepCreateDto?[] steps) => steps.ToList();

        private static string Message(Action action)
        {
            var ex = Assert.Throws<ArgumentException>(action);
            return ex.Message;
        }

        // ── Title ─────────────────────────────────────────────────────────────

        [Fact(DisplayName = "TC-WV01: Null or whitespace title — 'Workflow title is required.'")]
        public void Title_NullOrBlank_Throws()
        {
            Assert.Equal("Workflow title is required.", Message(() => WorkflowRequestValidator.ValidateTitle(null)));
            Assert.Equal("Workflow title is required.", Message(() => WorkflowRequestValidator.ValidateTitle("   ")));
        }

        [Fact(DisplayName = "TC-WV02: Title over 150 characters after trimming — rejected; exactly 150 accepted and trimmed")]
        public void Title_TooLong_Throws()
        {
            Assert.Equal("Workflow title must be 150 characters or fewer.",
                Message(() => WorkflowRequestValidator.ValidateTitle(new string('a', 151))));

            var ok = WorkflowRequestValidator.ValidateTitle("  " + new string('a', 150) + "  ");
            Assert.Equal(150, ok.Length);
        }

        // ── Description ───────────────────────────────────────────────────────

        [Fact(DisplayName = "TC-WV03: Description over 1000 characters — rejected; null becomes empty string")]
        public void Description_Rules()
        {
            Assert.Equal("Description must be 1000 characters or fewer.",
                Message(() => WorkflowRequestValidator.ValidateBody(new string('d', 1001), Steps(Good()), Roles)));

            Assert.Equal("", WorkflowRequestValidator.ValidateBody(null, Steps(Good()), Roles));
            Assert.Equal("hi", WorkflowRequestValidator.ValidateBody("  hi  ", Steps(Good()), Roles));
        }

        // ── Steps collection ──────────────────────────────────────────────────

        [Fact(DisplayName = "TC-WV04: Null or empty steps — 'A workflow needs at least one step.'")]
        public void Steps_NullOrEmpty_Throws()
        {
            Assert.Equal("A workflow needs at least one step.",
                Message(() => WorkflowRequestValidator.ValidateBody("d", null, Roles)));
            Assert.Equal("A workflow needs at least one step.",
                Message(() => WorkflowRequestValidator.ValidateBody("d", Steps(), Roles)));
        }

        [Fact(DisplayName = "TC-WV05: More than 20 steps — rejected; exactly 20 accepted")]
        public void Steps_TooMany_Throws()
        {
            var twenty = Enumerable.Range(0, 20).Select(_ => (WorkflowStepCreateDto?)Good()).ToList();
            WorkflowRequestValidator.ValidateBody("d", twenty, Roles);

            var twentyOne = Enumerable.Range(0, 21).Select(_ => (WorkflowStepCreateDto?)Good()).ToList();
            Assert.Equal("A workflow can have at most 20 steps.",
                Message(() => WorkflowRequestValidator.ValidateBody("d", twentyOne, Roles)));
        }

        // ── Per-step rules ────────────────────────────────────────────────────

        [Fact(DisplayName = "TC-WV06: Step name blank — 'Step {n}: name is required.' with 1-based position")]
        public void StepName_Blank_Throws()
        {
            var steps = Steps(Good(), Good("  "));
            Assert.Equal("Step 2: name is required.",
                Message(() => WorkflowRequestValidator.ValidateBody("d", steps, Roles)));
        }

        [Fact(DisplayName = "TC-WV07: Step name over 100 characters — rejected")]
        public void StepName_TooLong_Throws()
        {
            var steps = Steps(Good(new string('n', 101)));
            Assert.Equal("Step 1: name must be 100 characters or fewer.",
                Message(() => WorkflowRequestValidator.ValidateBody("d", steps, Roles)));
        }

        [Fact(DisplayName = "TC-WV08: Step instructions over 500 characters — rejected")]
        public void StepInstructions_TooLong_Throws()
        {
            var steps = Steps(new WorkflowStepCreateDto(1, 2, "Review", new string('i', 501), "GoBack", null));
            Assert.Equal("Step 1: instructions must be 500 characters or fewer.",
                Message(() => WorkflowRequestValidator.ValidateBody("d", steps, Roles)));
        }

        [Fact(DisplayName = "TC-WV09: Unknown approver role — 'Step {n}: the approver role was not found.'")]
        public void ApproverRole_Unknown_Throws()
        {
            var steps = Steps(new WorkflowStepCreateDto(1, 99, "Review", null, "GoBack", null));
            Assert.Equal("Step 1: the approver role was not found.",
                Message(() => WorkflowRequestValidator.ValidateBody("d", steps, Roles)));
        }

        [Theory(DisplayName = "TC-WV10: Reject action must be exactly GoBack or Cancel")]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("goback")]
        [InlineData("Skip")]
        public void RejectAction_Invalid_Throws(string? action)
        {
            var steps = Steps(new WorkflowStepCreateDto(1, 2, "Review", null, action, null));
            Assert.Equal("Step 1: reject action must be GoBack or Cancel.",
                Message(() => WorkflowRequestValidator.ValidateBody("d", steps, Roles)));
        }

        [Theory(DisplayName = "TC-WV11: Escalation outside 1..720 — rejected")]
        [InlineData(0)]
        [InlineData(-5)]
        [InlineData(721)]
        public void Escalation_OutOfRange_Throws(int hours)
        {
            var steps = Steps(new WorkflowStepCreateDto(1, 2, "Review", null, "Cancel", hours));
            Assert.Equal("Step 1: escalation must be between 1 and 720 hours.",
                Message(() => WorkflowRequestValidator.ValidateBody("d", steps, Roles)));
        }

        [Theory(DisplayName = "TC-WV12: Escalation null, 1 and 720 are accepted")]
        [InlineData(null)]
        [InlineData(1)]
        [InlineData(720)]
        public void Escalation_InRange_Accepted(int? hours)
        {
            var steps = Steps(new WorkflowStepCreateDto(1, 2, "Review", null, "Cancel", hours));
            WorkflowRequestValidator.ValidateBody("d", steps, Roles);
        }

        [Fact(DisplayName = "TC-WV13: Per-step rules run in table order (name, instructions, role, reject, escalation)")]
        public void StepRules_RunInTableOrder()
        {
            // Role, reject action and escalation are all invalid: the role message wins.
            var steps = Steps(new WorkflowStepCreateDto(1, 99, "Review", null, "Nope", 0));
            Assert.Equal("Step 1: the approver role was not found.",
                Message(() => WorkflowRequestValidator.ValidateBody("d", steps, Roles)));
        }

        // ── Status ────────────────────────────────────────────────────────────

        [Theory(DisplayName = "TC-WV14: PUT status must be exactly Draft, Active or Inactive")]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("active")]
        [InlineData("Archived")]
        public void UpdateStatus_Invalid_Throws(string? status)
        {
            Assert.Equal("Status must be Draft, Active or Inactive.",
                Message(() => WorkflowRequestValidator.ValidateUpdateStatus(status)));
        }

        [Fact(DisplayName = "TC-WV15: PUT valid statuses accepted")]
        public void UpdateStatus_Valid_Accepted()
        {
            WorkflowRequestValidator.ValidateUpdateStatus("Draft");
            WorkflowRequestValidator.ValidateUpdateStatus("Active");
            WorkflowRequestValidator.ValidateUpdateStatus("Inactive");
        }

        [Fact(DisplayName = "TC-WV16: POST status — null/Draft = Draft, Active = Active, else rejected")]
        public void CreateStatus_Resolution()
        {
            Assert.Equal("Draft", WorkflowRequestValidator.ResolveCreateStatus(null));
            Assert.Equal("Draft", WorkflowRequestValidator.ResolveCreateStatus("Draft"));
            Assert.Equal("Active", WorkflowRequestValidator.ResolveCreateStatus("Active"));
            Assert.Equal("Status must be Draft or Active when creating.",
                Message(() => WorkflowRequestValidator.ResolveCreateStatus("Inactive")));
            Assert.Equal("Status must be Draft or Active when creating.",
                Message(() => WorkflowRequestValidator.ResolveCreateStatus("")));
        }

        // ── Normalisation ─────────────────────────────────────────────────────

        [Fact(DisplayName = "TC-WV17: Normalise ignores client StepOrder and trims name/instructions")]
        public void Normalise_ByPosition()
        {
            var input = Steps(
                new WorkflowStepCreateDto(9, 2, "  A  ", "  x  ", "GoBack", 5),
                new WorkflowStepCreateDto(9, 3, "B", null, "Cancel", null));

            var result = WorkflowRequestValidator.Normalise(input);

            Assert.Equal(new[] { 1, 2 }, result.Select(s => s.StepOrder).ToArray());
            Assert.Equal("A", result[0].StepName);
            Assert.Equal("x", result[0].Description);
            Assert.Null(result[1].Description);
        }
    }
}
