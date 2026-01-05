# Move up to root
cd ..
git pull

# Rebuild with diagnostic version (includes BlockContextTestRunner)
.\build-with-runner.ps1

# Run with the test bundle from Compiler's output folder
# (Deploy target copies this to %COMMONAPPDATA%\Smalltalk-80\)
cd Smalltalk80.Interpreter
dotnet run -- --coldstart ..\Smalltalk80.Compiler\bin\Debug\net9.0\coldstart_with_runner.json
