#!/usr/bin/env python3
import os
import subprocess
import sys

# Change to the project directory
os.chdir('/home/user/RingOfRings')
print(f"Working directory: {os.getcwd()}")
print(f"Files: {os.listdir('.')}")

# Run dotnet build
print("\n=== Building RingOfRingsTests.csproj ===")
result = subprocess.run(
    ['dotnet', 'build', 'RingOfRingsTests.csproj'],
    capture_output=True,
    text=True
)
print("STDOUT:")
print(result.stdout)
print("STDERR:")
print(result.stderr)
print(f"Return code: {result.returncode}")

if result.returncode == 0:
    print("\n=== Running Tests ===")
    result = subprocess.run(
        ['dotnet', 'run', '--project', 'RingOfRingsTests.csproj'],
        capture_output=False,
        text=True
    )
    print(f"Test return code: {result.returncode}")
