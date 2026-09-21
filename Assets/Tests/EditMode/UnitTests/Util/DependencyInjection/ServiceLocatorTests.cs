using System;
using NUnit.Framework;
using Rebellion.Util.DependencyInjection;

namespace Rebellion.Tests.Util.DependencyInjection
{
    [TestFixture]
    public sealed class ServiceLocatorTests
    {
        /// <summary>
        /// Verifies a singleton is created only once within its locator.
        /// </summary>
        [Test]
        public void GetService_SingletonRequestedTwice_ReturnsSameInstance()
        {
            ServiceContainer container = new();
            container.AddSingleton<Dependency>();
            using ServiceLocator locator = container.BuildServiceLocator();

            Assert.AreSame(locator.GetService<Dependency>(), locator.GetService<Dependency>());
        }

        /// <summary>
        /// Verifies a runtime type resolves the same singleton as a generic request.
        /// </summary>
        [Test]
        public void GetService_RuntimeType_ReturnsRegisteredSingleton()
        {
            ServiceContainer container = new();
            container.AddSingleton<Dependency>();
            using ServiceLocator locator = container.BuildServiceLocator();

            Assert.AreSame(
                locator.GetService<Dependency>(),
                locator.GetService(typeof(Dependency))
            );
        }

        /// <summary>
        /// Verifies null runtime types cannot be resolved.
        /// </summary>
        [Test]
        public void GetService_NullType_ThrowsArgumentNullException()
        {
            using ServiceLocator locator = new ServiceContainer().BuildServiceLocator();

            Assert.Throws<ArgumentNullException>(() => locator.GetService(null));
        }

        /// <summary>
        /// Verifies a transient is constructed for every resolution.
        /// </summary>
        [Test]
        public void GetService_TransientRequestedTwice_ReturnsDifferentInstances()
        {
            ServiceContainer container = new();
            container.AddTransient<Dependency, Dependency>();
            using ServiceLocator locator = container.BuildServiceLocator();

            Assert.AreNotSame(locator.GetService<Dependency>(), locator.GetService<Dependency>());
        }

        /// <summary>
        /// Verifies registered constructor parameters are resolved recursively.
        /// </summary>
        [Test]
        public void GetService_NestedDependencies_InjectsRegisteredInstance()
        {
            ServiceContainer container = new();
            Dependency dependency = new();
            container.AddSingletonInstance(dependency);
            container.AddSingleton<DependentService>();
            using ServiceLocator locator = container.BuildServiceLocator();

            Assert.AreSame(dependency, locator.GetService<DependentService>().Dependency);
        }

        /// <summary>
        /// Verifies a missing constructor dependency fails clearly.
        /// </summary>
        [Test]
        public void GetService_UnregisteredDependency_ThrowsInvalidOperationException()
        {
            ServiceContainer container = new();
            container.AddSingleton<DependentService>();
            using ServiceLocator locator = container.BuildServiceLocator();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                locator.GetService<DependentService>()
            );
            StringAssert.Contains(nameof(Dependency), exception.Message);
        }

        /// <summary>
        /// Verifies a constructor cycle is rejected with its dependency path.
        /// </summary>
        [Test]
        public void GetService_CircularDependency_ThrowsInvalidOperationException()
        {
            ServiceContainer container = new();
            container.AddSingleton<FirstCycleService>();
            container.AddSingleton<SecondCycleService>();
            using ServiceLocator locator = container.BuildServiceLocator();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                locator.GetService<FirstCycleService>()
            );
            StringAssert.Contains(nameof(FirstCycleService), exception.Message);
            StringAssert.Contains(nameof(SecondCycleService), exception.Message);
        }

        /// <summary>
        /// Verifies an explicit factory may construct a service with additional arguments.
        /// </summary>
        [Test]
        public void GetService_FactoryRegistration_CreatesExpectedService()
        {
            ServiceContainer container = new();
            container.AddSingleton<Dependency>();
            container.AddSingleton(locator => new DependentService(
                locator.GetService<Dependency>()
            ));
            using ServiceLocator locator = container.BuildServiceLocator();

            Assert.AreSame(
                locator.GetService<Dependency>(),
                locator.GetService<DependentService>().Dependency
            );
        }

        /// <summary>
        /// Verifies factories must return a service of their registered type.
        /// </summary>
        [Test]
        public void GetService_FactoryReturnsNull_ThrowsInvalidOperationException()
        {
            ServiceContainer container = new();
            container.AddSingleton<Dependency>(_ => null);
            using ServiceLocator locator = container.BuildServiceLocator();

            Assert.Throws<InvalidOperationException>(() => locator.GetService<Dependency>());
        }

        /// <summary>
        /// Verifies constructor failures retain their original exception type.
        /// </summary>
        [Test]
        public void GetService_ConstructorThrows_PropagatesOriginalException()
        {
            ServiceContainer container = new();
            container.AddSingleton<FailingService>();
            using ServiceLocator locator = container.BuildServiceLocator();

            Assert.Throws<ArgumentException>(() => locator.GetService<FailingService>());
        }

        /// <summary>
        /// Verifies constructed services are disposed with their locator.
        /// </summary>
        [Test]
        public void Dispose_ConstructedSingleton_DisposesService()
        {
            ServiceContainer container = new();
            container.AddSingleton<DisposableService>();
            ServiceLocator locator = container.BuildServiceLocator();
            DisposableService service = locator.GetService<DisposableService>();

            locator.Dispose();

            Assert.IsTrue(service.IsDisposed);
        }

        /// <summary>
        /// Verifies separately constructed transient services are each disposed.
        /// </summary>
        [Test]
        public void Dispose_ConstructedTransients_DisposesEachInstance()
        {
            ServiceContainer container = new();
            container.AddTransient<DisposableService, DisposableService>();
            ServiceLocator locator = container.BuildServiceLocator();
            DisposableService first = locator.GetService<DisposableService>();
            DisposableService second = locator.GetService<DisposableService>();

            locator.Dispose();

            Assert.IsTrue(first.IsDisposed);
            Assert.IsTrue(second.IsDisposed);
        }

        /// <summary>
        /// Verifies externally supplied instances remain owned by the caller.
        /// </summary>
        [Test]
        public void Dispose_RegisteredInstance_DoesNotDisposeService()
        {
            ServiceContainer container = new();
            DisposableService service = new();
            container.AddSingletonInstance(service);
            ServiceLocator locator = container.BuildServiceLocator();
            locator.GetService<DisposableService>();

            locator.Dispose();

            Assert.IsFalse(service.IsDisposed);
        }

        /// <summary>
        /// Verifies a disposed locator cannot resolve further services.
        /// </summary>
        [Test]
        public void GetService_DisposedLocator_ThrowsObjectDisposedException()
        {
            ServiceContainer container = new();
            container.AddSingleton<Dependency>();
            ServiceLocator locator = container.BuildServiceLocator();
            locator.Dispose();

            Assert.Throws<ObjectDisposedException>(() => locator.GetService<Dependency>());
        }

        private sealed class Dependency
        {
            /// <summary>
            /// Creates a dependency for service-resolution tests.
            /// </summary>
            public Dependency() { }
        }

        private sealed class DependentService
        {
            public Dependency Dependency { get; }

            /// <summary>
            /// Creates a service that requires another registered service.
            /// </summary>
            /// <param name="dependency">The dependency to retain.</param>
            public DependentService(Dependency dependency)
            {
                Dependency = dependency;
            }
        }

        private sealed class FirstCycleService
        {
            /// <summary>
            /// Creates the first side of a circular dependency.
            /// </summary>
            /// <param name="second">The other side of the cycle.</param>
            public FirstCycleService(SecondCycleService second) { }
        }

        private sealed class SecondCycleService
        {
            /// <summary>
            /// Creates the second side of a circular dependency.
            /// </summary>
            /// <param name="first">The other side of the cycle.</param>
            public SecondCycleService(FirstCycleService first) { }
        }

        private sealed class FailingService
        {
            /// <summary>
            /// Fails while its constructor is invoked.
            /// </summary>
            /// <exception cref="ArgumentException">Always thrown for this test.</exception>
            public FailingService()
            {
                throw new ArgumentException("construction failed");
            }
        }

        private sealed class DisposableService : IDisposable
        {
            public bool IsDisposed { get; private set; }

            /// <summary>
            /// Creates a disposable service for ownership tests.
            /// </summary>
            public DisposableService() { }

            /// <summary>
            /// Records whether its owner disposed it.
            /// </summary>
            public void Dispose()
            {
                IsDisposed = true;
            }
        }
    }
}
