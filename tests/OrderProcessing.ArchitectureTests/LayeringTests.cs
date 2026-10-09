using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;
using OrderProcessing.Api.Controllers;
using OrderProcessing.Application.Services;
using OrderProcessing.Domain;
using OrderProcessing.Infrastructure.Persistence;

namespace OrderProcessing.ArchitectureTests;

public class LayeringTests
{
    private static readonly Assembly DomainAssembly = typeof(Order).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(IOrderService).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(AppDbContext).Assembly;
    private static readonly Assembly ApiAssembly = typeof(OrdersController).Assembly;

    [Fact]
    public void The_domain_depends_on_no_other_layer_and_no_orm()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "OrderProcessing.Application",
                "OrderProcessing.Infrastructure",
                "OrderProcessing.Api",
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FailureMessage(result));
    }

    [Fact]
    public void The_application_layer_does_not_depend_on_infrastructure_api_or_the_orm()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "OrderProcessing.Infrastructure",
                "OrderProcessing.Api",
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore",
                "Npgsql")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FailureMessage(result));
    }

    [Fact]
    public void The_infrastructure_layer_does_not_depend_on_the_api()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .ShouldNot()
            .HaveDependencyOn("OrderProcessing.Api")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FailureMessage(result));
    }

    [Fact]
    public void Controllers_do_not_reach_into_persistence()
    {
        var result = Types.InAssembly(ApiAssembly)
            .That().ResideInNamespace("OrderProcessing.Api.Controllers")
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "OrderProcessing.Infrastructure.Persistence")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FailureMessage(result));
    }

    [Fact]
    public void The_application_abstractions_are_implemented_in_infrastructure()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .That().ImplementInterface(typeof(OrderProcessing.Application.Abstractions.IUnitOfWork))
            .Or().ImplementInterface(typeof(OrderProcessing.Application.Abstractions.IOrderRepository))
            .Should()
            .ResideInNamespace("OrderProcessing.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FailureMessage(result));
    }

    private static string FailureMessage(TestResult result) =>
        result.FailingTypeNames is null or { Count: 0 }
            ? "expected the architecture rule to hold"
            : $"violating types: {string.Join(", ", result.FailingTypeNames)}";
}
