# Move up to root
cd ..
git pull

# Rebuild with standard Smalltalk-80.st
.\build-standard.ps1

# Run the tests
cd Smalltalk80.Interpreter
# cls
dotnet run -- 