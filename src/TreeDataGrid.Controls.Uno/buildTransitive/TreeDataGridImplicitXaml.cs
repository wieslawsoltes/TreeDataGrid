// MSBuild task (compiled by RoslynCodeTaskFactory from TreeDataGrid.Controls.Uno.targets) that
// adds explicit xmlns prefixes to unprefixed TreeDataGrid XAML for WinUI's markup compiler.
#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

public sealed class TreeDataGridRewriteImplicitXaml : Task
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    [Required] public ITaskItem[] InputFiles { get; set; }
    [Required] public string ControlsTypes { get; set; }
    [Required] public string PrimitivesTypes { get; set; }
    [Required] public string ProjectDirectory { get; set; }
    [Required] public string OutputRoot { get; set; }
    /// <summary>
    /// Rewrite only files with unprefixed TreeDataGrid names in TargetType or Setter.Property values.
    /// Uno's generator resolves unprefixed elements through [XmlnsDefinition] but not these string values.
    /// </summary>
    public bool TypeValuesOnly { get; set; }
    [Output] public ITaskItem[] RewrittenFiles { get; set; }

    public override bool Execute()
    {
        var types = CreateTypeMap(ControlsTypes, PrimitivesTypes);

        var rewritten = new List<ITaskItem>();
        foreach (var item in InputFiles)
        {
            var path = item.GetMetadata("FullPath");
            if (!File.Exists(path)) continue;
            var text = File.ReadAllText(path);
            if (text.IndexOf("TreeDataGrid", StringComparison.Ordinal) < 0 && text.IndexOf("CreateOptions", StringComparison.Ordinal) < 0) continue;

            byte[] output;
            try
            {
                output = Rewrite(text, types, TypeValuesOnly);
            }
            catch (XmlException exception)
            {
                Log.LogWarning(null, "TDGXAML001", null, path, exception.LineNumber, exception.LinePosition, 0, 0,
                    "TreeDataGrid could not add xmlns prefixes to this XAML file: {0}", exception.Message);
                continue;
            }
            if (output == null) continue;

            var link = item.GetMetadata("Link");
            if (string.IsNullOrEmpty(link)) link = MakeRelative(ProjectDirectory, path);
            var target = Path.GetFullPath(Path.Combine(OutputRoot, link));
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            if (!File.Exists(target) || !File.ReadAllBytes(target).SequenceEqual(output)) File.WriteAllBytes(target, output);

            var result = new TaskItem(target);
            item.CopyMetadataTo(result);
            result.SetMetadata("Link", link);
            result.SetMetadata("TreeDataGridOriginalItemSpec", item.ItemSpec);
            rewritten.Add(result);
        }
        RewrittenFiles = rewritten.ToArray();
        return !Log.HasLoggedErrors;
    }

    /// <summary>The rewritten file, or null when it uses no TreeDataGrid type without a prefix.</summary>
    public static byte[] Rewrite(string text, IDictionary<string, string> types, bool typeValuesOnly = false)
    {
        var document = XDocument.Parse(text, LoadOptions.PreserveWhitespace);
        var root = document.Root;
        var used = new HashSet<string>(StringComparer.Ordinal);
        // TargetType and Setter.Property values that need a prefix once it is declared.
        var pendingValues = new List<KeyValuePair<XAttribute, string>>();

        Func<string, string> clrOwner = name =>
        {
            var dot = name.IndexOf('.');
            string clr;
            return types.TryGetValue(dot < 0 ? name : name.Substring(0, dot), out clr) ? clr : null;
        };

        foreach (var element in root.DescendantsAndSelf().ToList())
        {
            var elementClr = element.Name.Namespace == Presentation ? clrOwner(element.Name.LocalName) : null;
            if (elementClr != null)
            {
                element.Name = XNamespace.Get("using:" + elementClr) + element.Name.LocalName;
                used.Add(elementClr);
            }

            var attributes = element.Attributes().ToList();
            var changed = false;
            for (var i = 0; i < attributes.Count; ++i)
            {
                var attribute = attributes[i];
                if (attribute.IsNamespaceDeclaration || attribute.Name.Namespace != XNamespace.None) continue;
                var local = attribute.Name.LocalName;
                var attachedClr = local.IndexOf('.') > 0 ? clrOwner(local) : null;
                if (attachedClr != null)
                {
                    // Attached property set as an attribute: TreeDataGrid.Something="...".
                    attributes[i] = new XAttribute(XNamespace.Get("using:" + attachedClr) + local, attribute.Value);
                    used.Add(attachedClr);
                    changed = true;
                    continue;
                }
                if (local != "TargetType" && !(local == "Property" && element.Name.LocalName == "Setter")) continue;
                var valueClr = attribute.Value.IndexOf(':') < 0 ? clrOwner(attribute.Value.Trim()) : null;
                if (valueClr == null) continue;
                pendingValues.Add(new KeyValuePair<XAttribute, string>(attribute, valueClr));
                used.Add(valueClr);
            }
            if (changed) element.ReplaceAttributes(attributes);
        }

        if (used.Count == 0 || (typeValuesOnly && pendingValues.Count == 0)) return null;

        // Declare one prefix per CLR namespace, reusing an existing declaration on the root.
        var prefixes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var clr in used.OrderBy(name => name, StringComparer.Ordinal))
        {
            var uri = "using:" + clr;
            var prefix = root.Attributes()
                .Where(a => a.IsNamespaceDeclaration && a.Name.Namespace == XNamespace.Xmlns && a.Value == uri)
                .Select(a => a.Name.LocalName).FirstOrDefault();
            if (prefix == null)
            {
                var stem = clr == "Uno.Controls" ? "tdg" : "tdgp";
                prefix = stem;
                for (var n = 1; root.Attribute(XNamespace.Xmlns + prefix) != null; ++n) prefix = stem + n;
                root.Add(new XAttribute(XNamespace.Xmlns + prefix, uri));
            }
            prefixes[clr] = prefix;
        }
        foreach (var pending in pendingValues)
            pending.Key.Value = prefixes[pending.Value] + ":" + pending.Key.Value.Trim();

        using (var stream = new MemoryStream())
        {
            var settings = new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(true),
                OmitXmlDeclaration = document.Declaration == null,
                NewLineHandling = NewLineHandling.None,
            };
            using (var writer = XmlWriter.Create(stream, settings)) document.Save(writer);
            return stream.ToArray();
        }
    }

    /// <summary>Maps each type name to its CLR namespace.</summary>
    public static IDictionary<string, string> CreateTypeMap(string controlsTypes, string primitivesTypes)
    {
        var types = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in controlsTypes.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)) types[name.Trim()] = "Uno.Controls";
        foreach (var name in primitivesTypes.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)) types[name.Trim()] = "Uno.Controls.Primitives";
        return types;
    }

    private static string MakeRelative(string directory, string path)
    {
        var baseUri = new Uri(Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar);
        var relative = Uri.UnescapeDataString(baseUri.MakeRelativeUri(new Uri(path)).ToString());
        return relative.Replace('/', Path.DirectorySeparatorChar);
    }
}
