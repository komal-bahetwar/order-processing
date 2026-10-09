using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using OrderProcessing.Domain;

namespace OrderProcessing.ArchitectureTests;

public class BoundaryTests
{
    private static readonly Assembly DomainAssembly = typeof(Order).Assembly;

    [Fact]
    public void The_domain_assembly_references_no_other_layer_or_orm()
    {
        var references = DomainAssembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToList();

        references.Should().NotContain(name =>
            name.StartsWith("OrderProcessing.", StringComparison.Ordinal) ||
            name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) ||
            name.StartsWith("Npgsql", StringComparison.Ordinal) ||
            name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal) ||
            name.StartsWith("Hangfire", StringComparison.Ordinal));
    }

    [Fact]
    public void The_domain_public_surface_exposes_no_floating_point()
    {
        var offenders = new List<string>();

        foreach (var type in DomainAssembly.GetExportedTypes())
        {
            foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                switch (member)
                {
                    case PropertyInfo property when UsesFloatingPoint(property.PropertyType):
                        offenders.Add($"{type.Name}.{property.Name}");
                        break;
                    case MethodInfo method when method.ReturnType != typeof(void) && UsesFloatingPoint(method.ReturnType):
                        offenders.Add($"{type.Name}.{method.Name}()");
                        break;
                    case MethodInfo method when method.GetParameters().Any(p => UsesFloatingPoint(p.ParameterType)):
                        offenders.Add($"{type.Name}.{method.Name}(...)");
                        break;
                }
            }
        }

        offenders.Should().BeEmpty();
    }

    [Fact]
    public void Order_exposes_no_public_mutator_for_lifecycle_fields()
    {
        string[] guarded = ["Id", "Status", "CreatedAt", "UpdatedAt", "Version", "TotalAmount"];

        foreach (var name in guarded)
        {
            var property = typeof(Order).GetProperty(name);
            property.Should().NotBeNull();
            (property!.SetMethod?.IsPublic ?? false).Should().BeFalse($"{name} must not have a public setter");
        }
    }

    [Fact]
    public void Order_has_no_public_item_mutation_method()
    {
        string[] forbidden = ["AddItem", "RemoveItem", "Add", "Remove", "Clear"];

        var methodNames = typeof(Order)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(method => method.Name);

        methodNames.Should().NotIntersectWith(forbidden);
        typeof(Order).GetProperty("Items")!.PropertyType.Should().BeAssignableTo<System.Collections.Generic.IReadOnlyCollection<OrderItem>>();
    }

    [Fact]
    public void The_build_and_package_policies_are_enabled()
    {
        var root = RepoRoot();

        File.ReadAllText(Path.Combine(root, "Directory.Build.props"))
            .Should().Contain("<TreatWarningsAsErrors>true</TreatWarningsAsErrors>");
        File.ReadAllText(Path.Combine(root, "Directory.Packages.props"))
            .Should().Contain("<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>");
        File.Exists(Path.Combine(root, "global.json")).Should().BeTrue();

        // Central package management: no project pins a version on a PackageReference.
        var projectFiles = Directory
            .EnumerateFiles(Path.Combine(root, "src"), "*.csproj", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(Path.Combine(root, "tests"), "*.csproj", SearchOption.AllDirectories));

        foreach (var project in projectFiles)
        {
            File.ReadAllText(project)
                .Should().NotContain("Version=", $"{Path.GetFileName(project)} must not pin a package version");
        }
    }

    [Fact]
    public void The_evaluated_build_properties_are_enforced()
    {
        var root = RepoRoot();
        var project = Path.Combine(root, "src", "OrderProcessing.Domain", "OrderProcessing.Domain.csproj");

        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("msbuild");
        startInfo.ArgumentList.Add(project);
        foreach (var property in new[] { "TargetFramework", "TreatWarningsAsErrors", "ManagePackageVersionsCentrally" })
        {
            startInfo.ArgumentList.Add($"-getProperty:{property}");
        }
        startInfo.ArgumentList.Add("-nologo");

        using var process = Process.Start(startInfo)!;
        var stdout = process.StandardOutput.ReadToEnd();
        if (!process.WaitForExit(120_000))
        {
            process.Kill(entireProcessTree: true);
            throw new InvalidOperationException("dotnet msbuild did not finish within the timeout.");
        }

        process.ExitCode.Should().Be(0, stdout);

        using var document = JsonDocument.Parse(stdout);
        var properties = document.RootElement.GetProperty("Properties");
        properties.GetProperty("TargetFramework").GetString().Should().Be("net10.0");
        properties.GetProperty("TreatWarningsAsErrors").GetString().Should().Be("true");
        properties.GetProperty("ManagePackageVersionsCentrally").GetString().Should().Be("true");
    }

    private static bool UsesFloatingPoint(Type type)
    {
        if (type == typeof(float) || type == typeof(double))
        {
            return true;
        }

        if (type.IsArray && type.GetElementType() is { } element)
        {
            return UsesFloatingPoint(element);
        }

        return type.IsGenericType && type.GetGenericArguments().Any(UsesFloatingPoint);
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "OrderProcessing.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate the repository root from " + AppContext.BaseDirectory);
    }
}
