#!/usr/bin/env python3
"""
Updates all references from Manager classes to System classes.
"""
import os
import re

# Files to update with new references
FILES_TO_UPDATE = [
    "Assets/Scripts/Managers/GameManager.cs",
    "Assets/Scripts/Game/Missions/Mission.cs",
    "Assets/Scripts/Game/Missions/MissionFactory.cs",
    "Assets/Scripts/Util/Extensions/IMovableExtensions.cs",
    "Assets/Scripts/Game/MovementState.cs",
    "Assets/Tests/EditMode/Managers/BlockadeManagerTests.cs",
    "Assets/Tests/EditMode/Managers/ManufacturingManagerTests.cs",
    "Assets/Tests/EditMode/Managers/UprisingManagerTests.cs",
]

# Mapping of Manager names to System names
RENAMES = {
    "BetrayalManager": "BetrayalSystem",
    "BlockadeManager": "BlockadeSystem",
    "CombatManager": "CombatSystem",
    "DeathStarManager": "DeathStarSystem",
    "FogOfWarManager": "FogOfWarSystem",
    "GameEventManager": "GameEventSystem",
    "JediManager": "JediSystem",
    "ManufacturingManager": "ManufacturingSystem",
    "MissionManager": "MissionSystem",
    "MovementManager": "MovementSystem",
    "ResearchManager": "ResearchSystem",
    "VictoryManager": "VictorySystem",
}

def update_file(filepath):
    """Update a file to use System classes instead of Manager classes."""
    if not os.path.exists(filepath):
        print(f"Skipping {filepath} (doesn't exist)")
        return

    with open(filepath, 'r') as f:
        content = f.read()

    original_content = content

    # Add using Rebellion.Systems if not present and file uses any System
    needs_systems_import = False
    for manager, system in RENAMES.items():
        if manager in content:
            needs_systems_import = True
            break

    if needs_systems_import and "using Rebellion.Systems;" not in content:
        # Find the last using statement
        lines = content.split('\n')
        last_using_idx = -1
        for i, line in enumerate(lines):
            if line.strip().startswith('using '):
                last_using_idx = i

        if last_using_idx >= 0:
            # Insert after last using
            lines.insert(last_using_idx + 1, "using Rebellion.Systems;")
            content = '\n'.join(lines)

    # Replace all Manager references with System
    for manager, system in RENAMES.items():
        content = re.sub(rf'\b{manager}\b', system, content)

    # Update test class names if this is a test file
    if "Tests.cs" in filepath:
        # Update namespace
        content = re.sub(
            r'namespace Rebellion\.Tests\.Managers',
            'namespace Rebellion.Tests.Systems',
            content
        )
        # Update test class names
        content = re.sub(r'ManagerTests\b', 'SystemTests', content)

    if content != original_content:
        with open(filepath, 'w') as f:
            f.write(content)
        print(f"Updated {filepath}")
    else:
        print(f"No changes needed for {filepath}")

def main():
    for filepath in FILES_TO_UPDATE:
        update_file(filepath)
    print("\nReference updates complete!")

if __name__ == "__main__":
    main()
