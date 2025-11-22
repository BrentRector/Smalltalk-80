# Move up to root
cd ..
git pull

# Rebuild with diagnostic version
.\build-standard.ps1

# Run the tests
cd Smalltalk80.Interpreter
# cls
dotnet run -- --coldstart ..\Smalltalk80.Compiler\bin\Debug\net9.0\coldstart.json