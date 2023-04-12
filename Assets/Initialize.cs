using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using UnityEngine;
using UnityEditor;

[InitializeOnLoad]
public class Startup
{
    static Startup()
    {
        TestGame();
    }

    static void TestGame()
    {
        // Generate a game given a summary.
        GameSummary summary = new GameSummary
        {
            Size = GameSize.Large,
            Difficulty = GameDifficulty.Medium,
            VictoryCondition = GameVictoryCondition.Headquarters,
            ResourceAvailability = GameResourceAvailability.Abundant,
            PlayerFactionID = "FNALL1",
        };

        Game game = GameBuilder.BuildGame(summary);
    }
}
