using System;
using NUnit.Framework;
using Rebellion.Util.DependencyInjection;

namespace Rebellion.Tests.Util.DependencyInjection
{
    [TestFixture]
    public sealed class ServiceContainerTests
    {
        [Test]
        public void AddSingleton_DuplicateService_ThrowsInvalidOperationException()
        {
            ServiceContainer container = new();
            container.AddSingleton<SimpleService>();

            Assert.Throws<InvalidOperationException>(() => container.AddSingleton<SimpleService>());
        }

        [Test]
        public void AddSingleton_RuntimeType_ResolvesRegisteredService()
        {
            ServiceContainer container = new();
            container.AddSingleton(typeof(SimpleService));
            using ServiceLocator locator = container.BuildServiceLocator();

            Assert.IsInstanceOf<SimpleService>(locator.GetService(typeof(SimpleService)));
        }

        [Test]
        public void AddSingleton_AbstractImplementation_ThrowsArgumentException()
        {
            ServiceContainer container = new();

            Assert.Throws<ArgumentException>(() =>
                container.AddSingleton<IExampleService, IExampleService>()
            );
        }

        [Test]
        public void AddSingleton_NullFactory_ThrowsArgumentNullException()
        {
            ServiceContainer container = new();

            Assert.Throws<ArgumentNullException>(() =>
                container.AddSingleton<SimpleService>((Func<IServiceLocator, SimpleService>)null)
            );
        }

        [Test]
        public void AddSingleton_MultiplePublicConstructors_ThrowsInvalidOperationException()
        {
            ServiceContainer container = new();

            Assert.Throws<InvalidOperationException>(() =>
                container.AddSingleton<AmbiguousService>()
            );
        }

        [Test]
        public void AddSingletonInstance_NullInstance_ThrowsArgumentNullException()
        {
            ServiceContainer container = new();

            Assert.Throws<ArgumentNullException>(() =>
                container.AddSingletonInstance<SimpleService>(null)
            );
        }

        [Test]
        public void BuildServiceLocator_TwoLocators_CachesSingletonsIndependently()
        {
            ServiceContainer container = new();
            container.AddSingleton<SimpleService>();
            using ServiceLocator first = container.BuildServiceLocator();
            using ServiceLocator second = container.BuildServiceLocator();

            Assert.AreNotSame(
                first.GetService<SimpleService>(),
                second.GetService<SimpleService>()
            );
        }

        private sealed class SimpleService
        {
            /// <summary>
            /// Creates a simple service for registration tests.
            /// </summary>
            public SimpleService() { }
        }

        private interface IExampleService { }

        private sealed class AmbiguousService
        {
            /// <summary>
            /// Creates a service without a dependency.
            /// </summary>
            public AmbiguousService() { }

            /// <summary>
            /// Creates a service with a dependency.
            /// </summary>
            /// <param name="dependency">The dependency to receive.</param>
            public AmbiguousService(SimpleService dependency)
            {
                _ = dependency ?? throw new ArgumentNullException(nameof(dependency));
            }
        }
    }
}
