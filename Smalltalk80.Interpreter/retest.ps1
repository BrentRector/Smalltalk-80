# Move up to root
cd ..
git pull

# Rebuild with standard Smalltalk-80.st (builds Compiler, then Interpreter which deploys files)
.\build-standard.ps1

# Run the interpreter (files are now deployed to well-known folders by Deploy target)
cd Smalltalk80.Interpreter
dotnet run
