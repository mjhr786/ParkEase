namespace ParkingApp.UnitTests.Architecture;

/// <summary>
/// Host Domain assembly module-boundary scan.
/// Business aggregates live in physical module Domain projects; their graph is enforced by
/// <see cref="ModuleProjectReferenceRules"/> and <see cref="LayeringArchitectureTests"/>.
/// This allowlist remains for any residual host-Domain module namespaces (currently none).
/// </summary>
internal static class ModuleBoundaryAllowlist
{
    /// <summary>Module namespaces in the host Domain assembly to scan for cross-module edges.</summary>
    public static readonly string[] ModuleNamespaces = Array.Empty<string>();

    public static readonly HashSet<string> AllowedTypeEdges = new(StringComparer.Ordinal)
    {
        // No cross-module domain edges remain in the host Domain assembly.
        // See docs/modular-monolith-boundary-audit.md (2026-07-19).
    };

    public static string EdgeKey(Type source, Type target) =>
        $"{GetSimpleFullName(source)} -> {GetSimpleFullName(target)}";

    public static string? GetModuleNamespace(Type type)
    {
        var ns = type.Namespace;
        if (ns is null)
            return null;

        foreach (var moduleNs in ModuleNamespaces)
        {
            if (ns == moduleNs || ns.StartsWith(moduleNs + ".", StringComparison.Ordinal))
                return moduleNs;
        }

        return null;
    }

    private static string GetSimpleFullName(Type type)
    {
        if (!type.IsGenericType)
            return type.FullName ?? type.Name;

        var def = type.GetGenericTypeDefinition().FullName ?? type.Name;
        return def;
    }
}





