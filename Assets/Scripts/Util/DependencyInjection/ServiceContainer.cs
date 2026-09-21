using System;
using System.Collections.Generic;
using System.Reflection;

namespace Rebellion.Util.DependencyInjection
{
    /// <summary>
    /// Declares services before constructing an independent locator scope.
    /// </summary>
    public sealed class ServiceContainer
    {
        private readonly Dictionary<Type, Registration> _registrations = new();

        /// <summary>
        /// Registers one lazily constructed service for the lifetime of a locator.
        /// </summary>
        /// <typeparam name="TService">The service type callers request.</typeparam>
        /// <typeparam name="TImplementation">The implementation to construct.</typeparam>
        public void AddSingleton<TService, TImplementation>()
            where TImplementation : TService
        {
            Add(typeof(TService), Registration.ForType(typeof(TImplementation), true));
        }

        /// <summary>
        /// Registers one concrete service for the lifetime of a locator.
        /// </summary>
        /// <typeparam name="TService">The service type to construct.</typeparam>
        public void AddSingleton<TService>()
            where TService : class
        {
            AddSingleton<TService, TService>();
        }

        /// <summary>
        /// Registers a service factory whose result is cached for one locator.
        /// </summary>
        /// <typeparam name="TService">The service type callers request.</typeparam>
        /// <param name="factory">Creates the service using the current locator.</param>
        public void AddSingleton<TService>(Func<IServiceLocator, TService> factory)
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            Add(typeof(TService), Registration.ForFactory(locator => factory(locator), true));
        }

        /// <summary>
        /// Registers a new instance for each resolution.
        /// </summary>
        /// <typeparam name="TService">The service type callers request.</typeparam>
        /// <typeparam name="TImplementation">The implementation to construct.</typeparam>
        public void AddTransient<TService, TImplementation>()
            where TImplementation : TService
        {
            Add(typeof(TService), Registration.ForType(typeof(TImplementation), false));
        }

        /// <summary>
        /// Registers an existing instance without transferring its ownership to a locator.
        /// </summary>
        /// <typeparam name="TService">The service type callers request.</typeparam>
        /// <param name="instance">The existing service instance.</param>
        public void AddSingletonInstance<TService>(TService instance)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            Add(typeof(TService), Registration.ForInstance(instance));
        }

        /// <summary>
        /// Builds a locator with its own cache of constructed singleton services.
        /// </summary>
        /// <returns>A new independent service locator.</returns>
        public ServiceLocator BuildServiceLocator()
        {
            return new ServiceLocator(new Dictionary<Type, Registration>(_registrations));
        }

        /// <summary>
        /// Adds one registration and rejects accidental replacement.
        /// </summary>
        /// <param name="serviceType">The type callers request.</param>
        /// <param name="registration">The service construction rule.</param>
        private void Add(Type serviceType, Registration registration)
        {
            if (_registrations.ContainsKey(serviceType))
                throw new InvalidOperationException(
                    $"Service {serviceType.FullName} is already registered."
                );

            _registrations.Add(serviceType, registration);
        }

        /// <summary>
        /// Stores one immutable service-construction rule.
        /// </summary>
        internal sealed class Registration
        {
            internal Type ImplementationType { get; }
            internal Func<IServiceLocator, object> Factory { get; }
            internal object Instance { get; }
            internal bool IsSingleton { get; }
            internal bool OwnsInstance { get; }

            /// <summary>
            /// Creates a service-construction rule.
            /// </summary>
            /// <param name="implementationType">The type constructed by reflection, if any.</param>
            /// <param name="factory">The explicit service factory, if any.</param>
            /// <param name="instance">An already constructed service, if any.</param>
            /// <param name="isSingleton">Whether constructed instances are cached.</param>
            /// <param name="ownsInstance">Whether the locator disposes constructed instances.</param>
            private Registration(
                Type implementationType,
                Func<IServiceLocator, object> factory,
                object instance,
                bool isSingleton,
                bool ownsInstance
            )
            {
                ImplementationType = implementationType;
                Factory = factory;
                Instance = instance;
                IsSingleton = isSingleton;
                OwnsInstance = ownsInstance;
            }

            /// <summary>
            /// Creates a registration for a constructor-injected implementation.
            /// </summary>
            /// <param name="implementationType">The implementation to construct.</param>
            /// <param name="isSingleton">Whether the result is cached.</param>
            /// <returns>The new registration.</returns>
            internal static Registration ForType(Type implementationType, bool isSingleton)
            {
                if (implementationType.IsAbstract || implementationType.IsInterface)
                    throw new ArgumentException(
                        $"Service implementation {implementationType.FullName} must be concrete.",
                        nameof(implementationType)
                    );

                ConstructorInfo[] constructors = implementationType.GetConstructors();
                if (constructors.Length != 1)
                    throw new InvalidOperationException(
                        $"Service implementation {implementationType.FullName} must have exactly one public constructor or use a factory registration."
                    );

                return new Registration(implementationType, null, null, isSingleton, true);
            }

            /// <summary>
            /// Creates a registration backed by an explicit factory.
            /// </summary>
            /// <param name="factory">The factory that creates the service.</param>
            /// <param name="isSingleton">Whether the result is cached.</param>
            /// <returns>The new registration.</returns>
            internal static Registration ForFactory(
                Func<IServiceLocator, object> factory,
                bool isSingleton
            ) => new Registration(null, factory, null, isSingleton, true);

            /// <summary>
            /// Creates a registration for an externally owned instance.
            /// </summary>
            /// <param name="instance">The instance to return.</param>
            /// <returns>The new registration.</returns>
            internal static Registration ForInstance(object instance) =>
                new Registration(null, null, instance, true, false);
        }
    }
}
