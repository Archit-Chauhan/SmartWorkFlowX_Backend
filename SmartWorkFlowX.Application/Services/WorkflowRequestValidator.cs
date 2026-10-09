using SmartWorkFlowX.Application.Dtos;

namespace SmartWorkFlowX.Application.Services
{
    /// <summary>
    /// Pure validation/normalisation of workflow create/update requests (no database, no clock).
    /// Invalid input throws ArgumentException, which the exception middleware maps to 400.
    /// Checks that need the database (title uniqueness, the set of existing role ids) are done by the
    /// caller; the role ids are passed in so this class stays unit-testable.
    /// </summary>
    public static class WorkflowRequestValidator
    {
        public const int MaxTitleLength = 150;
        public const int MaxDescriptionLength = 1000;
        public const int MaxSteps = 20;
        public const int MaxStepNameLength = 100;
        public const int MaxStepInstructionsLength = 500;
        public const int MinEscalationHours = 1;
        public const int MaxEscalationHours = 720;

        public const string DuplicateTitleMessage = "A workflow with this title already exists.";

        /// <summary>Rules 1-2: returns the trimmed title.</summary>
        public static string ValidateTitle(string? title)
        {
            var trimmed = (title ?? string.Empty).Trim();
            if (trimmed.Length == 0)
                throw new ArgumentException("Workflow title is required.");
            if (trimmed.Length > MaxTitleLength)
                throw new ArgumentException("Workflow title must be 150 characters or fewer.");
            return trimmed;
        }

        /// <summary>
        /// Rules 4-11 (description, steps). Steps are checked one at a time in array order, each against the
        /// rules in table order. Returns the trimmed description (empty string when null).
        /// </summary>
        public static string ValidateBody(
            string? description,
            IReadOnlyList<WorkflowStepCreateDto?>? steps,
            ISet<int> existingRoleIds)
        {
            var trimmedDescription = (description ?? string.Empty).Trim();
            if (trimmedDescription.Length > MaxDescriptionLength)
                throw new ArgumentException("Description must be 1000 characters or fewer.");

            if (steps == null || steps.Count == 0)
                throw new ArgumentException("A workflow needs at least one step.");
            if (steps.Count > MaxSteps)
                throw new ArgumentException("A workflow can have at most 20 steps.");

            for (int i = 0; i < steps.Count; i++)
            {
                int n = i + 1;
                var step = steps[i];

                var name = (step?.StepName ?? string.Empty).Trim();
                if (name.Length == 0)
                    throw new ArgumentException("Step " + n + ": name is required.");
                if (name.Length > MaxStepNameLength)
                    throw new ArgumentException("Step " + n + ": name must be 100 characters or fewer.");

                var instructions = (step!.Description ?? string.Empty).Trim();
                if (instructions.Length > MaxStepInstructionsLength)
                    throw new ArgumentException("Step " + n + ": instructions must be 500 characters or fewer.");

                if (!existingRoleIds.Contains(step.ApproverRoleId))
                    throw new ArgumentException("Step " + n + ": the approver role was not found.");

                if (step.OnRejectAction != "GoBack" && step.OnRejectAction != "Cancel")
                    throw new ArgumentException("Step " + n + ": reject action must be GoBack or Cancel.");

                if (step.EscalationHours.HasValue &&
                    (step.EscalationHours.Value < MinEscalationHours || step.EscalationHours.Value > MaxEscalationHours))
                    throw new ArgumentException("Step " + n + ": escalation must be between 1 and 720 hours.");
            }

            return trimmedDescription;
        }

        /// <summary>Rule 12 (PUT only): Status must be exactly Draft, Active or Inactive.</summary>
        public static void ValidateUpdateStatus(string? status)
        {
            if (status != "Draft" && status != "Active" && status != "Inactive")
                throw new ArgumentException("Status must be Draft, Active or Inactive.");
        }

        /// <summary>POST only: null or Draft = Draft, Active = Active, anything else is rejected.</summary>
        public static string ResolveCreateStatus(string? status)
        {
            if (status == null || status == "Draft") return "Draft";
            if (status == "Active") return "Active";
            throw new ArgumentException("Status must be Draft or Active when creating.");
        }

        /// <summary>Builds the stored steps: order 1..n by array position, trimmed name/instructions.</summary>
        public static List<WorkflowStepCreateDto> Normalise(IReadOnlyList<WorkflowStepCreateDto?> steps)
        {
            var result = new List<WorkflowStepCreateDto>();
            for (int i = 0; i < steps.Count; i++)
            {
                var s = steps[i]!;
                result.Add(new WorkflowStepCreateDto(
                    i + 1,
                    s.ApproverRoleId,
                    s.StepName!.Trim(),
                    s.Description == null ? null : s.Description.Trim(),
                    s.OnRejectAction,
                    s.EscalationHours));
            }
            return result;
        }
    }
}
