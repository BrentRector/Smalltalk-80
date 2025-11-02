| dict |
"Test dictionary growth mechanism when dictionary becomes full"
dict := Dictionary new: 4.  "Start with a small dictionary"
1 to: 10 do: [:i |
    dict at: ('key', i printString) put: i].
dict size printString, ' items added successfully' printString.