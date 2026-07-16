using System;
using System.IO;
using System.Text;

namespace SM.HeadlessCensus;

public static class ConceptCatalogArtifactWriter
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);

    public static string Serialize(ConceptCatalog catalog)
    {
        if (catalog == null)
        {
            throw new ArgumentNullException(nameof(catalog));
        }

        return BuildSpaceJson.Serialize(catalog) + "\n";
    }

    public static string Write(string outputDirectory, ConceptCatalog catalog)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("output directory is empty.", nameof(outputDirectory));
        }

        Directory.CreateDirectory(outputDirectory);
        var path = Path.Combine(outputDirectory, "concept_catalog_bt1.json");
        File.WriteAllText(path, Serialize(catalog), Utf8WithoutBom);
        return path;
    }
}
