using System.Reflection;
using FluentAssertions;
using ParkingApp.Identity.Domain.Enums;

namespace ParkingApp.UnitTests.Architecture;

/// <summary>
/// Detects new cross-module Domain type references.
/// Known edges from the baseline audit are allowlisted; unknown edges fail.
/// </summary>
public class ModuleBoundaryArchitectureTests
{
    private static readonly Assembly DomainAssembly = typeof(UserRole).Assembly;

    [Fact]
    public void Domain_CrossModule_TypeReferences_Must_Be_Allowlisted()
    {
        var violations = new List<string>();

        var moduleTypes = DomainAssembly.GetTypes()
            .Where(t => t.IsClass || t.IsInterface || t.IsEnum || t.IsValueType)
            .Where(t => ModuleBoundaryAllowlist.GetModuleNamespace(t) is not null)
            .ToList();

        foreach (var source in moduleTypes)
        {
            var sourceModule = ModuleBoundaryAllowlist.GetModuleNamespace(source)!;

            foreach (var target in GetReferencedTypes(source))
            {
                var targetModule = ModuleBoundaryAllowlist.GetModuleNamespace(target);
                if (targetModule is null || targetModule == sourceModule)
                    continue;

                // Skip self/nested noise
                if (target == source)
                    continue;

                var edge = ModuleBoundaryAllowlist.EdgeKey(source, target);
                if (!ModuleBoundaryAllowlist.AllowedTypeEdges.Contains(edge))
                {
                    violations.Add($"{edge}  (modules: {ShortModule(sourceModule)} -> {ShortModule(targetModule)})");
                }
            }
        }

        violations.Should().BeEmpty(
            "New cross-module Domain references must be added to ModuleBoundaryAllowlist only with an explicit migration decision. Violations:\n"
            + string.Join("\n", violations.OrderBy(v => v)));
    }

    [Fact]
    public void Allowlist_Entries_Should_Still_Exist_In_Domain()
    {
        // Guards against stale allowlist entries after refactors (typos / deleted types).
        var domainTypeNames = DomainAssembly.GetTypes()
            .Select(t => t.FullName)
            .Where(n => n is not null)
            .ToHashSet(StringComparer.Ordinal);

        var stale = new List<string>();
        foreach (var edge in ModuleBoundaryAllowlist.AllowedTypeEdges)
        {
            var parts = edge.Split(" -> ", StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
            {
                stale.Add($"Malformed edge: {edge}");
                continue;
            }

            // FullName for open generics can include `1 etc.; match prefix for non-generic entity types we listed.
            if (!domainTypeNames.Contains(parts[0]) && !domainTypeNames.Any(n => n!.StartsWith(parts[0], StringComparison.Ordinal)))
                stale.Add($"Missing source type: {parts[0]} (from {edge})");
            if (!domainTypeNames.Contains(parts[1]) && !domainTypeNames.Any(n => n!.StartsWith(parts[1], StringComparison.Ordinal)))
                stale.Add($"Missing target type: {parts[1]} (from {edge})");
        }

        stale.Should().BeEmpty("Allowlist references types that no longer exist:\n" + string.Join("\n", stale));
    }

    private static IEnumerable<Type> GetReferencedTypes(Type source)
    {
        var found = new HashSet<Type>();

        void Consider(Type? t)
        {
            if (t is null || t.IsGenericParameter)
                return;

            if (t.IsGenericType)
            {
                found.Add(t.GetGenericTypeDefinition());
                foreach (var arg in t.GetGenericArguments())
                    Consider(arg);
                // Also consider constructed element for non-definition lookups
                found.Add(t);
                return;
            }

            if (t.IsArray)
            {
                Consider(t.GetElementType());
                return;
            }

            found.Add(t);
        }

        Consider(source.BaseType);

        foreach (var i in source.GetInterfaces())
            Consider(i);

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        foreach (var prop in source.GetProperties(flags))
        {
            Consider(prop.PropertyType);
            foreach (var p in prop.GetIndexParameters())
                Consider(p.ParameterType);
        }

        foreach (var field in source.GetFields(flags))
            Consider(field.FieldType);

        foreach (var method in source.GetMethods(flags))
        {
            if (method.IsSpecialName)
                continue;
            Consider(method.ReturnType);
            foreach (var p in method.GetParameters())
                Consider(p.ParameterType);
        }

        foreach (var ctor in source.GetConstructors(flags))
        {
            foreach (var p in ctor.GetParameters())
                Consider(p.ParameterType);
        }

        return found.Where(t => t.Assembly == DomainAssembly);
    }

    private static string ShortModule(string moduleNs) =>
        moduleNs.Replace("ParkingApp.Domain.", "", StringComparison.Ordinal);
}





