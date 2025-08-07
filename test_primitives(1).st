"Test I/O primitives directly"
| result |

"Test file creation and writing"
result := Transcript primitiveExecute: 142 with: 'test.txt' with: 'w'.
result printNl.

"Test file writing"
result := Transcript primitiveExecute: 145 with: $H.
result printNl.

"Test file close"
result := Transcript primitiveExecute: 143.
result printNl.
