"Execute comprehensive StandardLibrary tests"

"Load test files and run all tests"
FileStream fileIn: 'StandardLibrary_Tests.st'.
FileStream fileIn: 'StandardLibrary_Boolean_Stream_Tests.st'.  
FileStream fileIn: 'StandardLibrary_Additional_Tests.st'.
FileStream fileIn: 'StandardLibrary_MasterTestRunner.st'.

"Execute the comprehensive test suite"
StandardLibraryMasterTestRunner execute.
