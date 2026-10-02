// MSBuild task (compiled by RoslynCodeTaskFactory from TreeDataGrid.Controls.Uno.targets) that
// gives unprefixed TreeDataGrid names in XAML explicit using: prefixes. The file is edited in
// place by insertions only: lines, formatting, entities and encoding stay as written, so compiler
// diagnostics in the copy point at the same lines as the source.
#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

public sealed class TreeDataGridRewriteImplicitXaml : Task
{
    private const string Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private const string XmlnsNamespace = "http://www.w3.org/2000/xmlns/";

    [Required] public ITaskItem[] InputFiles { get; set; }
    [Required] public string ControlsTypes { get; set; }
    [Required] public string PrimitivesTypes { get; set; }
    [Required] public string ProjectDirectory { get; set; }
    [Required] public string OutputRoot { get; set; }
    /// <summary>
    /// Prefix only TargetType and Setter.Property values: Uno's generator resolves unprefixed
    /// elements through [XmlnsDefinition] but not type names given as strings.
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
            var bytes = File.ReadAllBytes(path);
            var preamble = Preamble(bytes, out var encoding);
            var text = encoding.GetString(bytes, preamble.Length, bytes.Length - preamble.Length);
            if (text.IndexOf("TreeDataGrid", StringComparison.Ordinal) < 0 && text.IndexOf("CreateOptions", StringComparison.Ordinal) < 0) continue;

            string output;
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
            var content = preamble.Concat(encoding.GetBytes(output)).ToArray();
            if (!File.Exists(target) || !File.ReadAllBytes(target).SequenceEqual(content)) File.WriteAllBytes(target, content);

            var result = new TaskItem(target);
            item.CopyMetadataTo(result);
            result.SetMetadata("Link", link);
            result.SetMetadata("TreeDataGridOriginalItemSpec", item.ItemSpec);
            rewritten.Add(result);
        }
        RewrittenFiles = rewritten.ToArray();
        return !Log.HasLoggedErrors;
    }

    /// <summary>Maps each type name to its CLR namespace.</summary>
    public static IDictionary<string, string> CreateTypeMap(string controlsTypes, string primitivesTypes)
    {
        var types = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in controlsTypes.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)) types[name.Trim()] = "Uno.Controls";
        foreach (var name in primitivesTypes.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)) types[name.Trim()] = "Uno.Controls.Primitives";
        return types;
    }

    /// <summary>
    /// The XAML with explicit prefixes, or null when nothing needs one. Only insertions are made:
    /// a prefix before each affected name or value, and the xmlns declarations after the root
    /// element's name, so every line keeps its number.
    /// </summary>
    public static string Rewrite(string text, IDictionary<string, string> types, bool typeValuesOnly = false)
    {
        var lineStarts = LineStarts(text);
        Func<IXmlLineInfo, int> offset = info => lineStarts[info.LineNumber - 1] + info.LinePosition - 1;
        Func<string, string> clrOwner = name =>
        {
            var dot = name.IndexOf('.');
            string clr;
            return types.TryGetValue(dot < 0 ? name : name.Substring(0, dot), out clr) ? clr : null;
        };

        // Insertions as (offset, CLR namespace): the prefix is chosen once the whole file is known.
        var insertions = new List<KeyValuePair<int, string>>();
        var declaredPrefixes = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var rootPrefixes = new Dictionary<string, string>(StringComparer.Ordinal);
        var rootNameEnd = -1;

        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null };
        using (var reader = XmlReader.Create(new StringReader(text), settings))
        {
            var info = (IXmlLineInfo)reader;
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.EndElement)
                {
                    if (!typeValuesOnly && reader.Prefix.Length == 0 && reader.NamespaceURI == Presentation && clrOwner(reader.LocalName) is string endClr)
                        insertions.Add(new KeyValuePair<int, string>(offset(info), endClr));
                    continue;
                }
                if (reader.NodeType != XmlNodeType.Element) continue;

                var isRoot = rootNameEnd < 0;
                if (isRoot) rootNameEnd = offset(info) + reader.Name.Length;
                var elementName = reader.LocalName;
                if (!typeValuesOnly && reader.Prefix.Length == 0 && reader.NamespaceURI == Presentation && clrOwner(elementName) is string elementClr)
                    insertions.Add(new KeyValuePair<int, string>(offset(info), elementClr));

                for (var more = reader.MoveToFirstAttribute(); more; more = reader.MoveToNextAttribute())
                {
                    if (reader.NamespaceURI == XmlnsNamespace)
                    {
                        if (reader.Prefix.Length == 0) continue;
                        if (!declaredPrefixes.TryGetValue(reader.LocalName, out var uris)) declaredPrefixes[reader.LocalName] = uris = new List<string>();
                        uris.Add(reader.Value);
                        if (isRoot && reader.Value.StartsWith("using:", StringComparison.Ordinal)) rootPrefixes[reader.Value.Substring(6)] = reader.LocalName;
                        continue;
                    }
                    if (reader.Prefix.Length != 0) continue;
                    var name = reader.LocalName;
                    if (!typeValuesOnly && name.IndexOf('.') > 0 && clrOwner(name) is string attachedClr)
                    {
                        // Attached property set as an attribute: TreeDataGrid.Something="...".
                        insertions.Add(new KeyValuePair<int, string>(offset(info), attachedClr));
                        continue;
                    }
                    if (name != "TargetType" && !(name == "Property" && elementName == "Setter")) continue;
                    var value = reader.Value.Trim();
                    if (value.IndexOf(':') >= 0 || !(clrOwner(value) is string valueClr)) continue;
                    insertions.Add(new KeyValuePair<int, string>(ValueStart(text, offset(info) + name.Length), valueClr));
                }
                reader.MoveToElement();
            }
        }

        if (insertions.Count == 0) return null;

        // One prefix per CLR namespace: the root's own using: declaration when its prefix is not
        // redeclared elsewhere, otherwise a new prefix that the file does not declare anywhere.
        var prefixes = new Dictionary<string, string>(StringComparer.Ordinal);
        var declarations = new StringBuilder();
        foreach (var clr in insertions.Select(i => i.Value).Distinct().OrderBy(n => n, StringComparer.Ordinal))
        {
            if (rootPrefixes.TryGetValue(clr, out var existing) && declaredPrefixes[existing].Count == 1)
            {
                prefixes[clr] = existing;
                continue;
            }
            var stem = clr == "Uno.Controls" ? "tdg" : "tdgp";
            var prefix = stem;
            for (var n = 1; declaredPrefixes.ContainsKey(prefix) || prefixes.ContainsValue(prefix); ++n) prefix = stem + n;
            prefixes[clr] = prefix;
            declarations.Append(" xmlns:").Append(prefix).Append("=\"using:").Append(clr).Append('"');
        }

        var builder = new StringBuilder(text);
        var edits = insertions.Select(i => new KeyValuePair<int, string>(i.Key, prefixes[i.Value] + ":")).ToList();
        if (declarations.Length > 0) edits.Add(new KeyValuePair<int, string>(rootNameEnd, declarations.ToString()));
        foreach (var edit in edits.OrderByDescending(e => e.Key)) builder.Insert(edit.Key, edit.Value);
        return builder.ToString();
    }

    /// <summary>The offset of the first non-space character of the value whose attribute name ends at <paramref name="index"/>.</summary>
    private static int ValueStart(string text, int index)
    {
        while (text[index] != '=') ++index;
        ++index;
        while (char.IsWhiteSpace(text[index])) ++index;
        ++index; // opening quote
        while (char.IsWhiteSpace(text[index])) ++index;
        return index;
    }

    /// <summary>Line start offsets, counting line breaks as XML does (CRLF, CR or LF).</summary>
    private static List<int> LineStarts(string text)
    {
        var starts = new List<int> { 0 };
        for (var i = 0; i < text.Length; ++i)
        {
            if (text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n') ++i;
            if (text[i] == '\n' || text[i] == '\r') starts.Add(i + 1);
        }
        return starts;
    }

    private static byte[] Preamble(byte[] bytes, out Encoding encoding)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) { encoding = new UTF8Encoding(false); return bytes.Take(3).ToArray(); }
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE) { encoding = new UnicodeEncoding(false, false); return bytes.Take(2).ToArray(); }
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF) { encoding = new UnicodeEncoding(true, false); return bytes.Take(2).ToArray(); }
        encoding = new UTF8Encoding(false);
        return new byte[0];
    }

    private static string MakeRelative(string directory, string path)
    {
        var baseUri = new Uri(Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar);
        var relative = Uri.UnescapeDataString(baseUri.MakeRelativeUri(new Uri(path)).ToString());
        return relative.Replace('/', Path.DirectorySeparatorChar);
    }
}
