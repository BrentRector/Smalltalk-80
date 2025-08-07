$output = dotnet run --project Smalltalk80 -- --source test_smalltalk_dict.st --eval "Object new testSmalltalkDict" 2>&1
$output | Select-Object -Last 30
Write-Host "Exit code: $LASTEXITCODE"