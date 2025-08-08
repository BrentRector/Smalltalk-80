"Test dictionary expansion"
| dict |
dict _ MethodDictionary new.

"Add first entry"
dict at: #first put: 1.
Transcript show: 'Added first entry'; cr.

"Add second entry"
dict at: #second put: 2.
Transcript show: 'Added second entry'; cr.

"Add third entry - should trigger expansion if capacity is 2"
dict at: #third put: 3.
Transcript show: 'Added third entry'; cr.

"Add more entries to really test expansion"
dict at: #fourth put: 4.
dict at: #fifth put: 5.
dict at: #sixth put: 6.
Transcript show: 'Added entries 4-6'; cr.

"Verify all entries are accessible"
Transcript show: 'Verifying entries:'; cr.
Transcript show: 'first = ', (dict at: #first) printString; cr.
Transcript show: 'second = ', (dict at: #second) printString; cr.
Transcript show: 'third = ', (dict at: #third) printString; cr.
Transcript show: 'fourth = ', (dict at: #fourth) printString; cr.
Transcript show: 'fifth = ', (dict at: #fifth) printString; cr.
Transcript show: 'sixth = ', (dict at: #sixth) printString; cr.

Transcript show: 'Dictionary expansion test complete!'; cr.