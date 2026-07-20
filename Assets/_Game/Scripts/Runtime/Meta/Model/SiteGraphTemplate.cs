using System.Collections.Generic;

namespace SM.Meta.Model;

public sealed record SiteGraphTemplate(
    string Id,
    string SiteId,
    IReadOnlyList<SiteGraphNodeTemplate> Nodes);
