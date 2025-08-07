"Test if Float class variables were initialized properly"
| pi halfPi fourthPi twoPi radPerDeg ln2 sqrt2 sinCoef tanCoef expCoef |

'Testing Float class initialization:' printNl.
'====================================' printNl.

"Try to access Float class variables"
pi _ Float instVarNamed: #Pi.
halfPi _ Float instVarNamed: #Halfpi.
fourthPi _ Float instVarNamed: #Fourthpi.
twoPi _ Float instVarNamed: #Twopi.
radPerDeg _ Float instVarNamed: #RadiansPerDegree.
ln2 _ Float instVarNamed: #Ln2.
sqrt2 _ Float instVarNamed: #Sqrt2.
sinCoef _ Float instVarNamed: #SinCoefficients.
tanCoef _ Float instVarNamed: #TanCoefficients.
expCoef _ Float instVarNamed: #ExpPCoefficients.

'Pi: ' print.
pi isNil
	ifTrue: ['NIL - NOT INITIALIZED!' printNl]
	ifFalse: [pi printNl].

'Halfpi: ' print.
halfPi isNil
	ifTrue: ['NIL - NOT INITIALIZED!' printNl]
	ifFalse: [halfPi printNl].

'Fourthpi: ' print.
fourthPi isNil
	ifTrue: ['NIL - NOT INITIALIZED!' printNl]
	ifFalse: [fourthPi printNl].

'Twopi: ' print.
twoPi isNil
	ifTrue: ['NIL - NOT INITIALIZED!' printNl]
	ifFalse: [twoPi printNl].

'RadiansPerDegree: ' print.
radPerDeg isNil
	ifTrue: ['NIL - NOT INITIALIZED!' printNl]
	ifFalse: [radPerDeg printNl].

'Ln2: ' print.
ln2 isNil
	ifTrue: ['NIL - NOT INITIALIZED!' printNl]
	ifFalse: [ln2 printNl].

'Sqrt2: ' print.
sqrt2 isNil
	ifTrue: ['NIL - NOT INITIALIZED!' printNl]
	ifFalse: [sqrt2 printNl].

'SinCoefficients: ' print.
sinCoef isNil
	ifTrue: ['NIL - NOT INITIALIZED!' printNl]
	ifFalse: [
		sinCoef printString printNl
	].

'TanCoefficients: ' print.
tanCoef isNil
	ifTrue: ['NIL - NOT INITIALIZED!' printNl]
	ifFalse: [
		tanCoef printString printNl
	].

'ExpPCoefficients: ' print.
expCoef isNil
	ifTrue: ['NIL - NOT INITIALIZED!' printNl]
	ifFalse: [
		expCoef printString printNl
	].

'------------------------------------' printNl.
pi isNil
	ifTrue: ['ERROR: Float class was NOT properly initialized!' printNl]
	ifFalse: ['SUCCESS: Float class appears to be initialized!' printNl].