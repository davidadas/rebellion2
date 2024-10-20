// using System;
// using System.Collections.Generic;

// public static class ServiceLocatorHelper
// {
//     public static IServiceLocator GetServiceLocator(Game game)
//     {
//         ServiceLocator serviceLocator = new ServiceLocator();

//         List<BaseService> services = new List<BaseService>()
//         {
//             new LookupService(serviceLocator, game),
//             new MissionService(serviceLocator, game),
//             new UnitService(serviceLocator, game),
//             new EventService(serviceLocator, game),
//         };

//         foreach (BaseService service in services)
//         {
//             serviceLocator.RegisterService(service);
//         }

//         return serviceLocator;
//     }
// }
