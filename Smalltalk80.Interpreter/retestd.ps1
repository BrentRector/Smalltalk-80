# Move up to root
cd ..
git pull

# Rebuild with diagnostic version
.\build-with-runner.ps1

# Run the tests
cd Smalltalk80.Interpreter
# cls
dotnet run -- --coldstart ..\Smalltalk80.Interpreter\bin\Debug\net9.0-windows\Data\coldstart_with_runner.json