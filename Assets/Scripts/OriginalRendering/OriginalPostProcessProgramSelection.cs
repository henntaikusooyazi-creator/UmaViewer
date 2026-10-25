using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Gallop.Live.Rendering
{
    // Explicit ordered candidates and caller-qualified ranking dependencies.
    // No native layout, original constants, program bytes, or default profile.
    public sealed class OriginalPostProcessProgramSelection
    {
        public sealed class Profile
        {
            public readonly int SharedReward, UnrequestedPenalty;
            public Profile(int sharedReward, int unrequestedPenalty)
            {
                if (sharedReward <= 0 || unrequestedPenalty <= 0) throw new ArgumentOutOfRangeException(nameof(sharedReward));
                SharedReward = sharedReward; UnrequestedPenalty = unrequestedPenalty;
            }
        }
        public sealed class Candidate
        {
            public readonly string Id;
            public readonly bool Eligible;
            public readonly ReadOnlyCollection<string> Keywords;
            public Candidate(string id, IEnumerable<string> keywords, bool eligible)
            {
                ValidateToken(id); Id = id; Keywords = Array.AsReadOnly(Snapshot(keywords)); Eligible = eligible;
            }
        }
        public sealed class Decision
        {
            public readonly Candidate Program;
            public readonly ReadOnlyCollection<string> Requested;
            public readonly long Score;
            public readonly bool CacheHit;
            public int SourcePass => Owner.SourcePass;
            internal readonly OriginalPostProcessProgramSelection Owner;
            internal Decision(OriginalPostProcessProgramSelection owner, Candidate program, string[] requested, long score, bool hit)
            { Owner = owner; Program = program; Requested = Array.AsReadOnly(requested); Score = score; CacheHit = hit; }
        }
        readonly Candidate[] candidates;
        readonly HashSet<string> space;
        readonly HashSet<string> relevant;
        readonly Profile profile;
        readonly int maximumEntries;
        readonly Dictionary<string, Decision> cache = new Dictionary<string, Decision>(StringComparer.Ordinal);
        public readonly int SourcePass;
        public ReadOnlyCollection<string> DeclaredKeywords { get; }
        public ReadOnlyCollection<string> RelevantKeywords { get; }
        public int CacheCount => cache.Count;

        public OriginalPostProcessProgramSelection(int sourcePass, IEnumerable<string> declaredKeywords,
            IEnumerable<string> relevantKeywords, IEnumerable<Candidate> orderedCandidates, Profile profile, int maximumEntries)
        {
            if (sourcePass < 0 || maximumEntries < 1 || maximumEntries > 256) throw new ArgumentOutOfRangeException(nameof(sourcePass));
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            SourcePass = sourcePass; this.maximumEntries = maximumEntries;
            var names = Snapshot(declaredKeywords); DeclaredKeywords = Array.AsReadOnly(names);
            space = new HashSet<string>(names, StringComparer.Ordinal);
            var mask = Snapshot(relevantKeywords);
            if (mask.Any(k => !space.Contains(k))) throw new ArgumentException("Pass keywords must belong to the declared namespace.");
            RelevantKeywords = Array.AsReadOnly(mask); relevant = new HashSet<string>(mask, StringComparer.Ordinal);
            if (orderedCandidates == null) throw new ArgumentNullException(nameof(orderedCandidates));
            var rows = new List<Candidate>(); var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var candidate in orderedCandidates)
            {
                if (candidate == null || rows.Count >= 128 || !ids.Add(candidate.Id) || candidate.Keywords.Any(k => !relevant.Contains(k)))
                    throw new ArgumentException("Bounded unique candidates in the declared keyword space are required.");
                rows.Add(candidate);
            }
            if (rows.Count == 0) throw new ArgumentException("An explicit candidate table is required.");
            candidates = rows.ToArray();
        }
        public Decision Select(IEnumerable<string> effectiveKeywords)
        {
            var requested = Snapshot(effectiveKeywords).Where(relevant.Contains).ToArray();
            string key = string.Join("\n", requested);
            if (cache.TryGetValue(key, out var saved)) return new Decision(this, saved.Program, requested, saved.Score, true);
            var wanted = new HashSet<string>(requested, StringComparer.Ordinal);
            Candidate winner = null; long best = long.MinValue;
            foreach (var candidate in candidates)
            {
                if (!candidate.Eligible) continue;
                int shared = 0, extra = 0;
                foreach (var keyword in candidate.Keywords) { if (wanted.Contains(keyword)) shared++; else extra++; }
                long score = (long)shared * profile.SharedReward - (long)extra * profile.UnrequestedPenalty;
                if (score > best) { best = score; winner = candidate; } // Equal scores retain table order.
            }
            if (winner == null) throw new NotSupportedException("No explicitly eligible program.");
            if (cache.Count >= maximumEntries) throw new InvalidOperationException("Program selection cache bound reached.");
            var result = new Decision(this, winner, requested, best, false); cache.Add(key, result); return result;
        }
        internal bool HasCandidate(string id) => candidates.Any(c => c.Id == id);
        internal static void ValidateToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 128 || value.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
                throw new ArgumentException("A bounded keyword or program token is required.");
        }
        internal static string[] Snapshot(IEnumerable<string> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            var result = new HashSet<string>(StringComparer.Ordinal); int count = 0;
            foreach (var value in values)
            {
                ValidateToken(value);
                if (++count > 256 || !result.Add(value)) throw new ArgumentException("A bounded unique keyword set is required.");
            }
            return result.OrderBy(k => k, StringComparer.Ordinal).ToArray();
        }
    }
}
