@echo off
cd bin\Debug\net9.0
echo exit | Smalltalk80.Interpreter.exe repl --trace-methods > test_output.txt 2>&1
findstr /C:"EXECUTE SELECTOR" /C:"Result:" /C:"Status:" /C:"VALIDATION" test_output.txt
del test_output.txt