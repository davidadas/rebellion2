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
            GalaxySize = GameSize.Large,
            Difficulty = GameDifficulty.Easy,
            VictoryCondition = GameVictoryCondition.Headquarters,
            ResourceAvailability = GameResourceAvailability.Abundant,
            PlanetaryStart = GameStartingPlanets.Sparse,
            PlayerFactionID = "FNALL1",
        };
        GameBuilder builder = new GameBuilder(summary);
        Game game = builder.BuildGame();

        // Write the scene to Debug for inspection.
        XmlSerializer xmlSerializer = new XmlSerializer(typeof(Game));
        using (FileStream fileStream = new FileStream("test.xml", FileMode.Open))
        {
            xmlSerializer.Serialize(fileStream, game);
        }
    }
}
