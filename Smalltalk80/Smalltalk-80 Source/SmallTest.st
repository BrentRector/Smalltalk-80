!AltoFile methodsFor: 'private'!
beSnapshotSerialNumber: aByteArray leaderVirtualDiskAddr: anInteger
	<primitive: 135>
	newSize > oldSize ifTrue: [^self error: 'invalid extension'].
	self primitiveFailed! !