#!/bin/bash
cd /home/user/RingOfRings
pwd
echo "Building..."
dotnet build RingOfRingsTests.csproj
echo "Build exit code: $?"
echo "Running tests..."
dotnet run --project RingOfRingsTests.csproj
echo "Run exit code: $?"
