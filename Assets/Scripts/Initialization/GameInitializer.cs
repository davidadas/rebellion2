using DependencyInjectionExtensions;

public class GameInitializer
{
    private Game game;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="game"></param>
    public GameInitializer(Game game)
    {
        this.game = game;
    }

    /// <summary>
    /// 
    /// </summary>
    public void Initialize()
    {
        IServiceLocator serviceLocator = getServiceLocator();

        GameManager manager = new GameManager(serviceLocator, game);
    }

    /// <summary>
    /// 
    /// </summary>
    private IServiceLocator getServiceLocator()
    {
        // Create the service container for which to build out services.
        ServiceContainer serviceContainer = new ServiceContainer();

        // Add services.
        serviceContainer.AddSingleton<IEventService, EventService>();
        serviceContainer.AddSingleton<ILookupService, LookupService>();
        serviceContainer.AddSingleton<IMissionService, MissionService>();

        // Add the Game object.
        serviceContainer.AddSingletonInstance<Game>(game);

        // Build and return the service locator.
        return serviceContainer.BuildServiceLocator();
    }
}
