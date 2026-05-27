#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")"

SERVER="./Builds/LinuxServer/MultiplayerServer.x86_64"
if [[ ! -f "$SERVER" ]]; then
  echo "Server build not found: $SERVER"
  echo "Build it from Unity first: Practice 4/Build Linux Dedicated Server"
  exit 1
fi

chmod +x "$SERVER"
echo "WSL IP addresses:"
hostname -I
echo "Starting FishNet dedicated server..."
"$SERVER" -batchmode -nographics
