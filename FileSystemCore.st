"Modern File System Implementation for Smalltalk-80"
"Adapted from system image to work with modern C# file system primitives"

Object subclass: #File
	instanceVariableNames: 'fileDirectory fileName binary readWrite error handle'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Files-Streams'!

File comment:
'I represent a file on the host file system. I am a simplified version of the original Smalltalk-80 File class, adapted to work with modern file system primitives rather than Alto disk primitives.

Instance Variables:
	fileName		<String> - The name of the file
	fileDirectory	<FileDirectory> - The directory containing this file
	binary			<Boolean> - Whether file contains binary or text data
	readWrite		<Integer> - File access mode
	error			<Symbol> - Error condition if any
	handle			<Integer> - File handle from host system
'!

!File methodsFor: 'accessing'!
fileName
	"Answer the name of the receiver."
	^fileName!
fileName: aString
	"Set the name of the receiver."
	fileName _ aString!
directory
	"Answer the directory containing this file."
	^fileDirectory!
directory: aFileDirectory
	"Set the directory containing this file."
	fileDirectory _ aFileDirectory!
handle
	"Answer the file handle from the host system."
	^handle!
handle: anInteger
	"Set the file handle from the host system."
	handle _ anInteger! !

!File methodsFor: 'testing'!
isBinary
	"Answer whether this file contains binary data."
	^binary == true!
isText
	"Answer whether this file contains text data."
	^binary == false! !

!File methodsFor: 'file modes'!
binary
	"Set this file to binary mode."
	binary _ true!
text
	"Set this file to text mode."
	binary _ false!
readWrite: mode
	"Set the read/write mode."
	readWrite _ mode! !

!File methodsFor: 'converting'!
asFileStream
	"Answer a FileStream on this file."
	^FileStream on: self! !

!File methodsFor: 'file operations'!
open: mode
	"Open the file with the specified mode. Uses primitive 142."
	<primitive: 142>
	"Fallback implementation"
	handle _ FileDirectory openFile: fileName mode: mode.
	^handle notNil!
close
	"Close the file. Uses primitive 143."
	<primitive: 143>
	"Fallback implementation"
	handle notNil ifTrue: [
		FileDirectory closeFile: handle.
		handle _ nil]!
delete
	"Delete this file from the file system."
	<primitive: 149>
	"Fallback implementation - requires FileDirectory support"
	^self directory deleteFile: fileName! !

!File class methodsFor: 'instance creation'!
named: aString
	"Create a new file with the given name."
	^self new fileName: aString; yourself!
named: aString in: aDirectory
	"Create a new file with the given name in the specified directory."
	^self new fileName: aString; directory: aDirectory; yourself! !

Object subclass: #FileDirectory
	instanceVariableNames: 'directoryName'
	classVariableNames: 'DefaultDirectory'
	poolDictionaries: ''
	category: 'Files-Streams'!

FileDirectory comment:
'I represent a directory on the host file system. I provide methods for creating, accessing, and managing files within a directory.

This is a simplified modern implementation adapted from the original Smalltalk-80 FileDirectory class to work with modern file system primitives rather than Alto-specific disk operations.
'!

!FileDirectory methodsFor: 'accessing'!
directoryName
	"Answer the name of this directory."
	^directoryName!
directoryName: aString
	"Set the name of this directory."
	directoryName _ aString! !

!FileDirectory methodsFor: 'file creation'!
file: aFileName
	"Answer a FileStream on a file with the given name, creating it if necessary."
	^(self fileNamed: aFileName) asFileStream!
newFile: aFileName
	"Create a new file with the given name."
	| file |
	file _ File named: aFileName in: self.
	file open: 'w'.
	^file!
oldFile: aFileName
	"Open an existing file with the given name."
	| file |
	file _ File named: aFileName in: self.
	file open: 'r'.
	^file! !

!FileDirectory methodsFor: 'file operations'!
fileNamed: aString
	"Answer a File object for the file with the given name."
	^File named: aString in: self!
includesFileNamed: aString
	"Answer whether a file with the given name exists in this directory."
	<primitive: 149>
	"Fallback implementation"
	^false!
deleteFileNamed: aString
	"Delete the file with the given name."
	<primitive: 150>
	"Fallback implementation"
	^false! !

!FileDirectory methodsFor: 'directory operations'!
createDirectory: aString
	"Create a subdirectory with the given name."
	<primitive: 154>
	"Fallback implementation"
	^false!
isDirectory: aString
	"Answer whether the named entry is a directory."
	<primitive: 156>
	"Fallback implementation"
	^false!
entries
	"Answer a collection of the entries in this directory."
	<primitive: 157>
	"Fallback implementation"
	^#()! !

!FileDirectory class methodsFor: 'instance creation'!
default
	"Answer the default file directory."
	DefaultDirectory isNil ifTrue: [
		DefaultDirectory _ self new directoryName: '.'; yourself].
	^DefaultDirectory!
on: aString
	"Answer a FileDirectory for the named directory."
	^self new directoryName: aString; yourself! !

!FileDirectory class methodsFor: 'file operations'!
openFile: fileName mode: mode
	"Low-level file opening. This would be implemented using host system calls."
	"This is a placeholder for primitive file operations"
	^nil!
closeFile: handle
	"Low-level file closing."
	"This is a placeholder for primitive file operations"
	^nil! !

"Enhance FileStream with primitive support"
!FileStream methodsFor: 'file operations'!
open: mode
	"Open the underlying file with the specified mode."
	^self file open: mode!
reopen
	"Reopen the file."
	self close.
	^self open: (rwmode bitAnd: 3)! !
