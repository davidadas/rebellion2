#!/usr/bin/env python3
"""
Migrates Manager classes to Systems namespace.
"""
import os
import re

MANAGERS_DIR = "Assets/Scripts/Managers"
SYSTEMS_DIR = "Assets/Scripts/Systems"

# Managers to convert to Systems
MANAGERS_TO_MIGRATE = [
    "BetrayalManager",
    "BlockadeManager",
    "CombatManager",
    "DeathStarManager",
    "FogOfWarManager",
    "GameEventManager",
    "JediManager",
    "ManufacturingManager",
    "MissionManager",
    "MovementManager",
    "ResearchManager",
    "VictoryManager",
]

def migrate_manager_to_system(manager_name):
    """Convert a Manager class to a System class."""
    system_name = manager_name.replace("Manager", "System")
    manager_file = os.path.join(MANAGERS_DIR, f"{manager_name}.cs")
    system_file = os.path.join(SYSTEMS_DIR, f"{system_name}.cs")

    with open(manager_file, 'r') as f:
        content = f.read()

    # Replace class name
    content = re.sub(
        rf'\bpublic class {manager_name}\b',
        f'public class {system_name}',
        content
    )

    # Replace constructor name
    content = re.sub(
        rf'\bpublic {manager_name}\(',
        f'public {system_name}(',
        content
    )

    # Replace references to manager name in comments
    content = content.replace(f"new {manager_name}", f"new {system_name}")
    content = content.replace(f"{manager_name} is", f"{system_name} is")

    # Add namespace wrapper if not already present
    if "namespace" not in content:
        # Find where imports end (last using statement)
        lines = content.split('\n')
        last_using_idx = -1
        for i, line in enumerate(lines):
            if line.strip().startswith('using '):
                last_using_idx = i

        # Insert namespace after imports
        if last_using_idx >= 0:
            before_namespace = '\n'.join(lines[:last_using_idx + 1])
            after_namespace = '\n'.join(lines[last_using_idx + 1:])
            content = f"{before_namespace}\n\nnamespace Rebellion.Systems\n{{\n{after_namespace}\n}}"
        else:
            # No imports, just wrap the whole thing
            content = f"namespace Rebellion.Systems\n{{\n{content}\n}}"

    # Write to Systems directory
    with open(system_file, 'w') as f:
        f.write(content)

    print(f"Migrated {manager_name} -> {system_name}")

def main():
    # Create Systems directory if it doesn't exist
    os.makedirs(SYSTEMS_DIR, exist_ok=True)

    # Migrate each manager
    for manager in MANAGERS_TO_MIGRATE:
        migrate_manager_to_system(manager)

    print("\nMigration complete!")
    print(f"Created {len(MANAGERS_TO_MIGRATE)} system files in {SYSTEMS_DIR}")

if __name__ == "__main__":
    main()
