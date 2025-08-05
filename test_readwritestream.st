| stream result |

"Test ReadWriteStream basic functionality"
stream _ ReadWriteStream on: (String new: 100).

"Test writing"
stream nextPutAll: 'Hello'.
stream space.
stream nextPutAll: 'World'.

"Test repositioning and reading"
stream position: 0.
result _ stream next: 5.

Transcript show: 'ReadWriteStream test - Read result: '; show: result printString; cr.

"Test contents"
stream position: 0.
Transcript show: 'Full contents: '; show: stream contents printString; cr.

"Test close method"
stream close.

Transcript show: 'ReadWriteStream test completed successfully'; cr!
