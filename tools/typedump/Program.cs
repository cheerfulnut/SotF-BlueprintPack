// typedump <dll-or-folder> <type-name-regex> [--members-regex R] [--no-inherited-noise]
// Prints matching types (namespace, base type, interfaces), their fields with types, properties, methods with
// signatures, nested types and enum values. Metadata only: the assemblies are read as data, never loaded or run.
// Il2CppInterop noise (NativeFieldInfoPtr_*, NativeMethodInfoPtr_*, Il2CppClassPointerStore, .cctor) is hidden.
using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;

if (args.Length < 2) { Console.Error.WriteLine("usage: typedump <dll|folder> <type-regex> [--members R]"); return 2; }
var files = Directory.Exists(args[0]) ? Directory.GetFiles(args[0], "*.dll") : new[] { args[0] };
var typeRx = new Regex(args[1]);
Regex memberRx = null;
for (int i = 2; i < args.Length - 1; i++) if (args[i] == "--members") memberRx = new Regex(args[i + 1], RegexOptions.IgnoreCase);

foreach (var file in files.OrderBy(f => f))
{
  try
  {
    using var fs = File.OpenRead(file);
    using var pe = new PEReader(fs);
    if (!pe.HasMetadata) continue;
    var md = pe.GetMetadataReader();
    var prov = new NameProvider(md);
    foreach (var th in md.TypeDefinitions)
    {
        var t = md.GetTypeDefinition(th);
        var full = FullName(md, t);
        if (!typeRx.IsMatch(full)) continue;
        var baseName = t.BaseType.IsNil ? "" : " : " + TypeName(md, prov, t.BaseType);
        var ifaces = t.GetInterfaceImplementations().Select(h => TypeName(md, prov, md.GetInterfaceImplementation(h).Interface)).ToList();
        Console.WriteLine($"\n## {full}{baseName}{(ifaces.Count > 0 ? ", " + string.Join(", ", ifaces) : "")}   [{Path.GetFileName(file)}]");
        bool isEnum = baseName.EndsWith("System.Enum");
        foreach (var fh in t.GetFields())
        {
            var f = md.GetFieldDefinition(fh);
            var n = md.GetString(f.Name);
            if (n.StartsWith("NativeFieldInfoPtr_") || n.StartsWith("NativeMethodInfoPtr_") || n == "value__") continue;
            if (memberRx != null && !memberRx.IsMatch(n)) continue;
            if (isEnum)
            {
                var c = f.GetDefaultValue();
                var val = c.IsNil ? "" : " = " + ReadConst(md, md.GetConstant(c));
                Console.WriteLine($"   enum {n}{val}");
                continue;
            }
            var ft = f.DecodeSignature(prov, null);
            var st = (f.Attributes & FieldAttributes.Static) != 0 ? "static " : "";
            Console.WriteLine($"   field {st}{ft} {n}");
        }
        foreach (var ph in t.GetProperties())
        {
            var p = md.GetPropertyDefinition(ph);
            var n = md.GetString(p.Name);
            if (memberRx != null && !memberRx.IsMatch(n)) continue;
            var sig = p.DecodeSignature(prov, null);
            var acc = p.GetAccessors();
            Console.WriteLine($"   prop {sig.ReturnType} {n} {{{(acc.Getter.IsNil ? "" : " get;")}{(acc.Setter.IsNil ? "" : " set;")} }}");
        }
        foreach (var mh in t.GetMethods())
        {
            var m = md.GetMethodDefinition(mh);
            var n = md.GetString(m.Name);
            if (n == ".cctor" || n.StartsWith("get_") || n.StartsWith("set_")) continue;
            if (memberRx != null && !memberRx.IsMatch(n)) continue;
            var sig = m.DecodeSignature(prov, null);
            var pnames = m.GetParameters().Select(h => md.GetParameter(h)).Where(p => p.SequenceNumber > 0)
                          .OrderBy(p => p.SequenceNumber).Select(p => md.GetString(p.Name)).ToList();
            var ps = sig.ParameterTypes.Select((pt, i) => $"{pt} {(i < pnames.Count ? pnames[i] : "p" + i)}");
            var st = (m.Attributes & MethodAttributes.Static) != 0 ? "static " : "";
            Console.WriteLine($"   method {st}{sig.ReturnType} {n}({string.Join(", ", ps)})");
        }
        foreach (var nh in t.GetNestedTypes())
            Console.WriteLine($"   nested {md.GetString(md.GetTypeDefinition(nh).Name)}");
    }
  }
  catch (Exception e) when (e is BadImageFormatException or InvalidOperationException)
  {
      Console.Error.WriteLine($"(skipped {Path.GetFileName(file)}: {e.Message})");
  }
}
return 0;

static string FullName(MetadataReader md, TypeDefinition t)
{
    var name = md.GetString(t.Name);
    if (t.IsNested) return FullName(md, md.GetTypeDefinition(t.GetDeclaringType())) + "+" + name;
    var ns = md.GetString(t.Namespace);
    return ns.Length > 0 ? ns + "." + name : name;
}

static string TypeName(MetadataReader md, NameProvider prov, EntityHandle h) => h.Kind switch
{
    HandleKind.TypeDefinition => prov.GetTypeFromDefinition(md, (TypeDefinitionHandle)h, 0),
    HandleKind.TypeReference => prov.GetTypeFromReference(md, (TypeReferenceHandle)h, 0),
    HandleKind.TypeSpecification => md.GetTypeSpecification((TypeSpecificationHandle)h).DecodeSignature(prov, null),
    _ => "?"
};

static string ReadConst(MetadataReader md, Constant c)
{
    var r = md.GetBlobReader(c.Value);
    return c.TypeCode switch
    {
        ConstantTypeCode.Int32 => r.ReadInt32().ToString(),
        ConstantTypeCode.UInt32 => r.ReadUInt32().ToString(),
        ConstantTypeCode.Int16 => r.ReadInt16().ToString(),
        ConstantTypeCode.UInt16 => r.ReadUInt16().ToString(),
        ConstantTypeCode.Byte => r.ReadByte().ToString(),
        ConstantTypeCode.SByte => r.ReadSByte().ToString(),
        ConstantTypeCode.Int64 => r.ReadInt64().ToString(),
        ConstantTypeCode.UInt64 => r.ReadUInt64().ToString(),
        _ => c.TypeCode.ToString()
    };
}

sealed class NameProvider : ISignatureTypeProvider<string, object>
{
    private readonly MetadataReader _md;
    public NameProvider(MetadataReader md) => _md = md;
    public string GetPrimitiveType(PrimitiveTypeCode c) => c.ToString().ToLowerInvariant();
    public string GetTypeFromDefinition(MetadataReader md, TypeDefinitionHandle h, byte k)
    {
        var t = md.GetTypeDefinition(h);
        var n = md.GetString(t.Name);
        if (t.IsNested) return GetTypeFromDefinition(md, t.GetDeclaringType(), k) + "+" + n;
        var ns = md.GetString(t.Namespace);
        return ns.Length > 0 ? ns + "." + n : n;
    }
    public string GetTypeFromReference(MetadataReader md, TypeReferenceHandle h, byte k)
    {
        var t = md.GetTypeReference(h);
        var n = md.GetString(t.Name);
        if (t.ResolutionScope.Kind == HandleKind.TypeReference)
            return GetTypeFromReference(md, (TypeReferenceHandle)t.ResolutionScope, k) + "+" + n;
        var ns = md.GetString(t.Namespace);
        return ns.Length > 0 ? ns + "." + n : n;
    }
    public string GetTypeFromSpecification(MetadataReader md, object ctx, TypeSpecificationHandle h, byte k)
        => md.GetTypeSpecification(h).DecodeSignature(this, ctx);
    public string GetSZArrayType(string e) => e + "[]";
    public string GetArrayType(string e, ArrayShape s) => e + "[" + new string(',', s.Rank - 1) + "]";
    public string GetByReferenceType(string e) => "ref " + e;
    public string GetPointerType(string e) => e + "*";
    public string GetPinnedType(string e) => e;
    public string GetGenericInstantiation(string g, ImmutableArray<string> a)
    {
        var tick = g.IndexOf('`');
        return (tick > 0 ? g[..tick] : g) + "<" + string.Join(", ", a) + ">";
    }
    public string GetGenericMethodParameter(object ctx, int i) => "!!" + i;
    public string GetGenericTypeParameter(object ctx, int i) => "!" + i;
    public string GetModifiedType(string m, string u, bool r) => u;
    public string GetFunctionPointerType(MethodSignature<string> s) => "fnptr";
}
