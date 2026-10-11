using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Rebellion.SceneGraph;

/// <summary>
/// Caches generated model dimensions without loading Unity meshes or rendering objects.
/// Metadata is read beside the resolved GLB so a mod cannot inherit another model's bounds.
/// </summary>
public sealed class ContentModelMetadata
{
    private readonly ContentFileResolver fileResolver;
    private readonly Dictionary<string, ModelDimensions> dimensions = new Dictionary<
        string,
        ModelDimensions
    >(StringComparer.Ordinal);

    /// <summary>Creates a model-metadata cache for one resolved content pack.</summary>
    /// <param name="resolver">The pack's layered file resolver.</param>
    public ContentModelMetadata(ContentFileResolver resolver)
    {
        fileResolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    }

    /// <summary>Resolves a node's runtime dimensions from its model and intentional uniform scale.</summary>
    /// <param name="node">The node whose derived dimensions are refreshed.</param>
    public void ResolveSize(BaseSceneNode node)
    {
        if (node == null)
            throw new ArgumentNullException(nameof(node));
        node.ModelSize = string.IsNullOrWhiteSpace(node.ModelPath)
            ? null
            : GetDimensions(node.ModelPath, node.ModelScale);
    }

    /// <summary>Returns independent, scaled dimensions from cached model-local metadata.</summary>
    /// <param name="address">The model's content address.</param>
    /// <param name="scale">The intentional uniform scale applied to its imported hierarchy.</param>
    /// <returns>The resolved dimensions in battle units.</returns>
    public ModelDimensions GetDimensions(string address, float scale)
    {
        if (string.IsNullOrWhiteSpace(address))
            throw new ArgumentException("A model address is required.", nameof(address));
        if (!IsPositive(scale))
            throw new ArgumentOutOfRangeException(nameof(scale));
        if (!dimensions.TryGetValue(address, out ModelDimensions size))
        {
            string modelPath =
                fileResolver.ResolveFile(address, ".glb")
                ?? throw new FileNotFoundException($"Content model is missing: {address}");
            string metadataPath = Path.ChangeExtension(modelPath, ".bounds.xml");
            size = ReadDimensions(metadataPath);
            dimensions.Add(address, size);
        }
        ModelDimensions result = new ModelDimensions
        {
            Width = size.Width * scale,
            Height = size.Height * scale,
            Depth = size.Depth * scale,
        };
        if (!IsPositive(result.Width) || !IsPositive(result.Height) || !IsPositive(result.Depth))
            throw new InvalidDataException($"Scaled model dimensions are invalid: {address}");
        return result;
    }

    /// <summary>Reads the strict generated sidecar format without resolving another content layer.</summary>
    /// <param name="path">The sidecar beside the selected model.</param>
    /// <returns>The positive model-local dimensions.</returns>
    private static ModelDimensions ReadDimensions(string path)
    {
        using XmlReader reader = XmlReader.Create(
            path,
            new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }
        );
        XElement root = XElement.Load(reader);
        string[] fields = { "SourceHash", "Width", "Height", "Depth" };
        if (
            root.Name != "ModelBounds"
            || root.HasAttributes
            || !root.Elements().Select(element => element.Name.ToString()).SequenceEqual(fields)
            || root.Elements().Any(element => element.HasAttributes || element.HasElements)
        )
            throw new InvalidDataException($"Invalid generated model metadata: {path}");
        string hash = root.Element("SourceHash").Value;
        if (hash.Length != 64 || hash.Any(character => !Uri.IsHexDigit(character)))
            throw new InvalidDataException($"Invalid model source hash: {path}");
        return new ModelDimensions
        {
            Width = ReadDimension(root, "Width", path),
            Height = ReadDimension(root, "Height", path),
            Depth = ReadDimension(root, "Depth", path),
        };
    }

    /// <summary>Reads and validates one generated dimension.</summary>
    /// <param name="root">The validated metadata root.</param>
    /// <param name="name">The dimension element name.</param>
    /// <param name="path">The source file for diagnostics.</param>
    /// <returns>A finite positive dimension.</returns>
    private static float ReadDimension(XElement root, string name, string path)
    {
        if (
            !float.TryParse(
                root.Element(name).Value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float value
            ) || !IsPositive(value)
        )
            throw new InvalidDataException($"Invalid {name} in model metadata: {path}");
        return value;
    }

    /// <summary>Checks a dimension or scale before it is used for deployment or rendering.</summary>
    /// <param name="value">The value to validate.</param>
    /// <returns>True for finite positive values.</returns>
    private static bool IsPositive(float value) => value > 0f && !float.IsInfinity(value);
}
