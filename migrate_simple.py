#!/usr/bin/env python3
"""
Simple migration: just rename classes, no namespace wrappers.
"""
import os
import re
import shutil

MANAGERS_DIR = "Assets/Scripts/Managers"
SYSTEMS_DIR = "Assets/Scripts/Systems"

MANAGERS = [
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

for manager in MANAGERS:
    system = manager.replace("Manager", "System")
    src = os.path.join(MANAGERS_DIR, f"{manager}.cs")
    dst = os.path.join(SYSTEMS_DIR, f"{system}.cs")

    with open(src, 'r') as f:
        content = f.read()

    # Replace class name and constructor
    content = re.sub(rf'\bpublic class {manager}\b', f'public class {system}', content)
    content = re.sub(rf'\bpublic {manager}\(', f'public {system}(', content)
    content = content.replace(f"new {manager}", f"new {system}")
    content = content.replace(f"{manager} is", f"{system} is")

    with open(dst, 'w') as f:
        f.write(content)

    print(f"Created {system}.cs")

print("\nDone!")
