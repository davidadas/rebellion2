using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace Rebellion.Util.DependencyInjection
{
    /// <summary>
    /// Resolves constructor dependencies and owns services created for one runtime scope.
    /// </summary>
    public sealed class ServiceLocator : IServiceLocator, IDisposable
    {
        private readonly Dictionary<Type, ServiceContainer.Registration> _registrations;
        private readonly Dictionary<Type, object> _singletons = new();
        private readonly List<Type> _resolutionPath = new();
        private readonly List<IDisposable> _ownedServices = new();
        private bool _isDisposed;

        /// <summary>
        /// Creates a locator from a fixed set of service registrations.
        /// </summary>
        /// <param name="registrations">The available service-construction rules.</param>
        internal ServiceLocator(Dictionary<Type, ServiceContainer.Registration> registrations)
        {
            _registrations =
                registrations ?? throw new ArgumentNullException(nameof(registrations));
            foreach (KeyValuePair<Type, ServiceContainer.Registration> entry in registrations)
            {
                if (entry.Value.Instance != null)
                    _singletons.Add(entry.Key, entry.Value.Instance);
            }
        }

        /// <summary>
        /// Resolves one registered service by its requested type.
        /// </summary>
        /// <typeparam name="T">The requested service type.</typeparam>
        /// <returns>The resolved service instance.</returns>
        public T GetService<T>() => (T)GetService(typeof(T));

        /// <summary>
        /// Resolves one registered service by its requested runtime type.
        /// </summary>
        /// <param name="serviceType">The requested service type.</param>
        /// <returns>The resolved service instance.</returns>
        public object GetService(Type serviceType)
        {
            if (serviceType == null)
                throw new ArgumentNullException(nameof(serviceType));
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(ServiceLocator));
            if (_singletons.TryGetValue(serviceType, out object singleton))
                return singleton;
            if (
                !_registrations.TryGetValue(
                    serviceType,
                    out ServiceContainer.Registration registration
                )
            )
                throw new InvalidOperationException(
                    $"Service {serviceType.FullName} is not registered."
                );

            if (_resolutionPath.Contains(serviceType))
                throw new InvalidOperationException(
                    $"Circular service dependency: {string.Join(" -> ", _resolutionPath.Concat(new[] { serviceType }).Select(type => type.Name))}."
                );

            _resolutionPath.Add(serviceType);
            try
            {
                object service = CreateService(registration);
                if (service == null || !serviceType.IsInstanceOfType(service))
                    throw new InvalidOperationException(
                        $"Registration for {serviceType.FullName} returned an invalid instance."
                    );

                if (registration.IsSingleton)
                    _singletons.Add(serviceType, service);
                if (registration.OwnsInstance && service is IDisposable disposable)
                    _ownedServices.Add(disposable);

                return service;
            }
            finally
            {
                _resolutionPath.RemoveAt(_resolutionPath.Count - 1);
            }
        }

        /// <summary>
        /// Disposes owned services in reverse construction order.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            for (int index = _ownedServices.Count - 1; index >= 0; index--)
                _ownedServices[index].Dispose();
            _ownedServices.Clear();
            _singletons.Clear();
        }

        /// <summary>
        /// Creates a service using its explicit factory or only public constructor.
        /// </summary>
        /// <param name="registration">The service-construction rule.</param>
        /// <returns>The newly constructed service.</returns>
        private object CreateService(ServiceContainer.Registration registration)
        {
            if (registration.Factory != null)
                return registration.Factory(this);

            ConstructorInfo constructor = registration.ImplementationType.GetConstructors()[0];
            object[] arguments = constructor
                .GetParameters()
                .Select(parameter => GetService(parameter.ParameterType))
                .ToArray();
            try
            {
                return constructor.Invoke(arguments);
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
        }
    }
}
