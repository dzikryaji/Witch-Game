using System.Collections.Generic;

namespace Witch.Infrastructure.Content
{
    public sealed class ContentValidationReport
    {
        public ContentValidationReport(IReadOnlyList<string> errors)
        {
            Errors = new List<string>(errors).AsReadOnly();
        }

        public bool IsValid => Errors.Count == 0;
        public IReadOnlyList<string> Errors { get; }
    }
}
