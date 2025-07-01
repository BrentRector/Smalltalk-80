grammar Smalltalk;

//----------------- Parser rules -----------------

// A complete method: a messagePattern header plus optional temporaries/statements
method: messagePattern ( temporaries? statements)? EOF;

// Method header: unary, binary(name), or keyword:name segments
messagePattern:
	unarySelector
	| binarySelector variableName
	| ( IDENTIFIER ':' variableName)+;

expression: (variableName ASSIGN_ARROW)* (
		primary
		| messageExpression
		| cascadedMessageExpression
	);

messageExpression:
	unaryObjectDescription (
		unarySelector
		| binarySelector unaryObjectDescription
		| keyword unaryObjectDescription
	)*;

cascadedMessageExpression:
	messageExpression (
		'.' (
			unarySelector
			| binarySelector unaryObjectDescription
			| keyword binaryObjectDescription
		)
	)+;

primary: variableName | literal | block | '(' expression ')';

unaryObjectDescription:
	primary
	| unaryObjectDescription unarySelector;

binaryObjectDescription:
	unaryObjectDescription
	| binaryObjectDescription binarySelector unaryObjectDescription;

keywordExpression:
	binaryObjectDescription (keyword binaryObjectDescription)*;

statements: statement ( '.' statement)*;
statement: '!'? expression;

block:
	'[' (':' variableName)* ('|' variableName* '|')? statements ']';

temporaries: '|' variableName* '|';

literal:
	number
	| symbolConstant
	| characterConstant
	| STRING
	| arrayConstant;

array:
	'(' (number | symbol | STRING | characterConstant | array)* ')';
arrayConstant: '#' array;

//----------------- Lexer rules -----------------

ASSIGN_ARROW: '<-' | '←';

IDENTIFIER: LETTER ( LETTER | DIGIT)*;
variableName: IDENTIFIER;
unarySelector: IDENTIFIER;

binarySelector: '-' | SPECIAL | SPECIAL SPECIAL;

keyword: ( IDENTIFIER ':')+;

symbol: IDENTIFIER | binarySelector | keyword;
symbolConstant: '#' symbol;

number: (DIGIT+ 'r')? '-'? DIGIT+ ('.' DIGIT+)? ('e' '-'? DIGIT+)?;

characterConstant: '$' ( CHARACTER | '\'' | '"');

STRING: '\'' ( CHARACTER | '\'\'')* '\'';

COMMENT: '"' ( CHARACTER | '""' | '\'')* '"' -> skip;

WS: [ \t\r\n]+ -> skip;

// fragments

fragment DIGIT: [0-9];
fragment LETTER: [A-Za-z];
fragment SPECIAL: [+/\\*~<>=@%|&?!];
fragment LEFT_ARROW: '\u2190'; // ← LEFTWARDS ARROW
fragment UP_ARROW: '\u2191'; // ↑ UPWARDS ARROW
fragment CHARACTER:
	DIGIT
	| LETTER
	| SPECIAL
	| [\[\]{}(),;\$\#\:]
	| LEFT_ARROW
	| UP_ARROW;