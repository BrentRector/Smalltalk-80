!ActionMenu class methodsFor: 'confirmation'!
confirm  "ActionMenu confirm"
	^ (ActionMenu labels: 'confirm\abort' withCRs selectors: nil) startUp = 1! !File subclass: #AltoFile
	instanceVariableNames: 'writeTime pageAddresses leader '
	classVariableNames: ''
	poolDictionaries: 'AltoFilePool '
	category: 'Files-Xerox Alto'!
AltoFile comment:
'A concrete example of class File.

Instance Variables:
	writeTime	<Time>
	pageAddresses	<RunArray> of page addresses
	leader		<Integer> address of the page where the leader information is stored
	'!
