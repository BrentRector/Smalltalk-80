# Rebuild and Test Procedure

## Step 1: Clean and Rebuild Compiler
```powershell
cd "d:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80.Compiler"
dotnet clean
dotnet build
```

**Verify**: Check build output shows "Build succeeded" and DLL timestamp is recent

## Step 2: Regenerate Cold Start Bundle
```powershell
cd "d:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk-80 Source"

# Compile the combined source file (compiler will use updated DLL from Step 1)
dotnet run --project ..\Smalltalk80.Compiler\Smalltalk80.Compiler.csproj -- Smalltalk-80.st Tests\BlockContextTest.st

# Verify coldstart.json was updated (check timestamp)
ls -l bin\Debug\net9.0\coldstart.json

# Copy to test bundle location
copy bin\Debug\net9.0\coldstart.json ..\bin\Debug\net9.0\coldstart_with_tests.json
```

**Verify**: The console output from compilation should be MINIMAL (we removed debug output).
If you see lots of `[EMIT]` or `[TempFrameAllocator]` messages, the old compiler is still running.

## Step 3: Rebuild Interpreter (Optional - only if interpreter code changed)
```powershell
cd "d:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80.Interpreter"
dotnet clean
dotnet build
```

## Step 4: Run Test
```powershell
cd "d:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80.Interpreter"
dotnet run -- --coldstart ..\bin\Debug\net9.0\coldstart_with_tests.json --skip-init-expressions --execute "BlockContextTest testAllCombinations"
```

**Expected Debug Output** (from updated code):
```
[Primitive 80 BlockCopy] argCount=1, argStartIndex=<some number>
```

**NOT** (old code):
```
[Primitive 80 BlockCopy] args[0]=SmallInteger(1) (raw=0x0003), argCount=1
```

## What Went Wrong

The issue is that when you ran the compiler to regenerate the bundle, it was using the OLD compiled DLL that didn't have the updated code to push 2 arguments.

Running `dotnet run` on the Compiler project will use the COMPILED DLL from `bin/Debug/net9.0/`, not directly compile from source each time. If you made changes to BytecodeCompiler.cs but didn't rebuild the Compiler project with `dotnet build`, those changes never made it into the DLL.

## Troubleshooting

If after Step 2 you still see the OLD debug output format, it means:
- The Compiler project wasn't rebuilt properly
- Check the timestamp on `Smalltalk80.Compiler\bin\Debug\net9.0\Smalltalk80.Compiler.dll`
- It should be AFTER your code changes

You can force a clean rebuild:
```powershell
cd Smalltalk80.Compiler
rm -r bin\
rm -r obj\
dotnet build
```
