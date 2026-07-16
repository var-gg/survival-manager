using System.Collections.Generic;

namespace SM.HeadlessCensus;

public sealed record ConceptRecipe(
    string RecipeId,
    string BuildId,
    string FormationSignature,
    IReadOnlyList<string> ComponentIds);
