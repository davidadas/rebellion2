#!/bin/bash
UNITY="/c/Program Files/Unity/Hub/Editor/6000.4.0f1/Editor/Unity.exe"
PROJ="C:/Users/David/git/_review_ai"
TAG="$1"; shift
for SEED in 55555 12345 24680; do
  echo "=== [$TAG] seed $SEED start $(date +%H:%M:%S) ==="
  "$UNITY" -batchmode -nographics -projectPath "$PROJ" \
    -executeMethod HeadlessSimulationRunner.RunDefaultSimulation \
    -simTicks 2000 -simSeed $SEED -simOut "SimulationResults/${TAG}-seed${SEED}.json" \
    -logFile "SimulationResults/${TAG}-seed${SEED}.log"
  echo "=== [$TAG] seed $SEED exit=$? end $(date +%H:%M:%S) ==="
done
echo "ALL DONE [$TAG]"
