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
        GameSummary summary = new GameSummary();

        // Generate a game given a summary.
        summary.Size = GameSize.Large;
        summary.Difficulty = GameDifficulty.Medium;
        summary.VictoryCondition = GameVictoryCondition.Headquarters;
        summary.ResourceAvailability = GameResourceAvailability.Abundant;
        summary.PlayerFactionID = "FNALL1";

        Game game = GameBuilder.BuildGame(summary);
    }
}
