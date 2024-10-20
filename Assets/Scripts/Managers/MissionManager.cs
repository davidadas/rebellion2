using System.Collections.Generic;
using System.Linq;
using System;
using DependencyInjectionExtensions;

/// <summary>
/// Manager for handling missions in the game.
/// This includes scheduling missions and rescheduling mission events.
/// </summary>
public class MissionManager
{
    protected Game game;
    protected IServiceLocator serviceLocator;

    public MissionManager(IServiceLocator serviceLocator, Game game)
    {
        this.game = game;
        this.serviceLocator = serviceLocator;
    }
}
