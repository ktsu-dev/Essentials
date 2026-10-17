## v2.12.0 (minor)

Changes since v2.11.0:

- Merge remote-tracking branch 'origin/main' into fix/66-atomic-store (Claude)
- Bound the retry loops explicitly and use Assert.Contains (Claude)
- Retry a read that lands on a Windows replace in progress (Claude)
- Make file-backed StoreAsync safe under concurrent writers and readers [patch] (Claude)
- Throw from Serialize when TrySerialize fails [minor] (Claude)
- Remove the key when InMemory stores null, as the file providers do [patch] (Claude)
- Merge main into fix/72-newtonsoft-leave-writer-open (Claude)
- Compute LogNormal variance with ExpM1 so a small sigma keeps its digits [patch] (Claude)
- Build the persistence test's paths with Path.Join (Claude)
- Register each category's default last in AddEssentials [patch] (Claude)
- Drop the .gitignore the SDK build rewrote (Claude)
- Ignore unmatched keys in the YAML serialization provider [patch] (Claude)
- Cover the Newtonsoft TrySerialize disposed-writer and IOException paths (Claude)
- Leave the caller's writer open in the Newtonsoft serialization provider [patch] (Claude)
- Format console log timestamps with the invariant culture [patch] (Claude)
- Construct and store a missing value type in RetrieveOrCreateAsync [patch] (Claude)
- Declare SurvivalFunction on Triangular, Uniform, Bernoulli and Categorical [patch] (Claude)

