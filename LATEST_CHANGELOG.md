## v2.11.1 (patch)

Changes since v2.11.0:

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

