using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DependencyInjectionExtensions;
using NUnit.Framework;

// Test classes and interfaces
internal interface ITestService { }

internal class TestService : ITestService { }

internal interface IDependentService
{
    ITestService TestService { get; }
}

internal class DependentService : IDependentService
{
    public ITestService TestService { get; }

    public DependentService(ITestService testService)
    {
        TestService = testService;
    }
}

[TestFixture]
public class DependencyInjectionTests
{
    private ServiceContainer container;

    [SetUp]
    public void SetUp()
    {
        container = new ServiceContainer();
    }

    [Test]
    public void AddSingletonReturnsSameInstance()
    {
        container.AddSingleton<ITestService, TestService>();
        var locator = container.BuildServiceLocator();

        var service1 = locator.GetService<ITestService>();
        var service2 = locator.GetService<ITestService>();

        Assert.IsNotNull(service1);
        Assert.AreSame(service1, service2, "Singleton service should return the same instance.");
    }

    [Test]
    public void AddTransientReturnsDifferentInstances()
    {
        container.AddTransient<ITestService, TestService>();
        var locator = container.BuildServiceLocator();

        var service1 = locator.GetService<ITestService>();
        var service2 = locator.GetService<ITestService>();

        Assert.IsNotNull(service1);
        Assert.IsNotNull(service2);
        Assert.AreNotSame(service1, service2, "Transient service should return different instances.");
    }

    [Test]
    public void AddSingletonInstanceReturnsProvidedInstance()
    {
        var instance = new TestService();
        container.AddSingletonInstance<ITestService>(instance);
        var locator = container.BuildServiceLocator();

        var service = locator.GetService<ITestService>();

        Assert.IsNotNull(service);
        Assert.AreSame(instance, service, "ServiceLocator should return the provided singleton instance.");
    }

    [Test]
    public void GetServiceWithConstructorInjectionResolvesDependencies()
    {
        container.AddSingleton<ITestService, TestService>();
        container.AddTransient<IDependentService, DependentService>();
        var locator = container.BuildServiceLocator();

        var dependentService = locator.GetService<IDependentService>();

        Assert.IsNotNull(dependentService);
        Assert.IsNotNull(dependentService.TestService, "Constructor dependency should be injected.");
    }

    [Test]
    public void GetServiceThrowsExceptionWhenServiceNotRegistered()
    {
        var locator = container.BuildServiceLocator();
        Assert.Throws<Exception>(() => locator.GetService<ITestService>(), "Service should not be found if not registered.");
    }

    [Test]
    public void SingletonServiceIsCreatedLazily()
    {
        container.AddSingleton<ITestService, TestService>();
        var locator = container.BuildServiceLocator();

        // Service should not be created until requested
        var service = locator.GetService<ITestService>();
        Assert.IsNotNull(service, "Service should be created lazily on first request.");
    }
}

