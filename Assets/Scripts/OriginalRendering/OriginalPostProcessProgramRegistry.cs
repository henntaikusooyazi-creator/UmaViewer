using System;
using System.Collections.Generic;

namespace Gallop.Live.Rendering
{
    // Explicit ownership: identical IDs in different tables or passes are independent.
    // The caller qualifies each program and retains ownership of its Material.
    public sealed class OriginalPostProcessProgramRegistry
    {
        public sealed class Registration
        {
            internal readonly OriginalPostProcessProgramSelection Catalog;
            internal readonly string CandidateId;
            internal readonly OriginalPostProcessChain.Program Program;
            public Registration(OriginalPostProcessProgramSelection catalog, string candidateId,
                OriginalPostProcessChain.Program program)
            {
                Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
                OriginalPostProcessProgramSelection.ValidateToken(candidateId);
                if (!catalog.HasCandidate(candidateId)) throw new ArgumentException("Candidate does not belong to this catalog.");
                Program = program ?? throw new ArgumentNullException(nameof(program));
                Program.Validate(); CandidateId = candidateId;
            }
        }
        readonly Dictionary<OriginalPostProcessProgramSelection, Dictionary<string, OriginalPostProcessChain.Program>> tables
            = new Dictionary<OriginalPostProcessProgramSelection, Dictionary<string, OriginalPostProcessChain.Program>>();
        public int Count { get; }
        public OriginalPostProcessProgramRegistry(IEnumerable<Registration> registrations)
        {
            if (registrations == null) throw new ArgumentNullException(nameof(registrations));
            int count = 0;
            foreach (var row in registrations)
            {
                if (row == null || ++count > 128) throw new ArgumentException("One to 128 explicit registrations are required.");
                if (!tables.TryGetValue(row.Catalog, out var programs))
                    tables.Add(row.Catalog, programs = new Dictionary<string, OriginalPostProcessChain.Program>(StringComparer.Ordinal));
                if (programs.ContainsKey(row.CandidateId)) throw new ArgumentException("Duplicate registration in the same catalog.");
                programs.Add(row.CandidateId, row.Program);
            }
            if (count == 0) throw new ArgumentException("An explicit registration is required.");
            Count = count;
        }
        public OriginalPostProcessChain.Program Resolve(OriginalPostProcessProgramSelection.Decision decision)
        {
            if (decision == null) throw new ArgumentNullException(nameof(decision));
            if (!tables.TryGetValue(decision.Owner, out var programs) || !programs.TryGetValue(decision.Program.Id, out var program))
                throw new NotSupportedException("The selected program has no registration in its own catalog.");
            program.Validate(); return program;
        }
    }
}
