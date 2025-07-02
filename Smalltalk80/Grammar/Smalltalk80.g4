grammar Smalltalk80;

//----------------- Parser rules ----------------- fragments

fragment DIGIT: [0-9];
fragment DIGITS: [0-9]+;
fragment LETTER: [A-Za-z];
fragment QUOTE: '\'';
fragment LEFT_ARROW: '\u2190'; // ← LEFTWARDS ARROW
fragment UP_ARROW: '\u2191'; // ↑ UPWARDS ARROW

SPECIALCHARACTER: [+/\\*~<>=@%|&?!];
CHARACTER:
	DIGIT
	| LETTER
	| SPECIALCHARACTER
	| '['
	| ']'
	| '{'
	| '}'
	| '('
	| ')'
	| LEFT_ARROW
	| UP_ARROW
	| ','
	| ';'
	| '$'
	| '!'
	| '#'
	| ':';

identifier: IDENTIFIER;
string: STRING;

symbol: identifier | binarySelector | keyword+;
symbolConstant: '#' symbol;
characterConstant: '$' ( CHARACTER | '\'' | '"');

array:
	'(' (NUMBER | symbol | string | characterConstant | array)* ')';
arrayConstant: '#' array;

literal:
	NUMBER
	| symbolConstant
	| characterConstant
	| string
	| arrayConstant;

variableName: IDENTIFIER;
unarySelector: IDENTIFIER;

binarySelector: '-' | SPECIALCHARACTER SPECIALCHARACTER?;

keyword: IDENTIFIER ':';

primary: variableName | literal | block | '(' expression ')';

unaryObjectDescription:
	primary
	| unaryObjectDescription unarySelector;

binaryObjectDescription:
	unaryObjectDescription
	| binaryObjectDescription binarySelector unaryObjectDescription;

keywordExpression:
	binaryObjectDescription (keyword binaryObjectDescription)+;

messageExpression:
	unaryObjectDescription unarySelector
	| binaryObjectDescription binarySelector unaryObjectDescription
	| keywordExpression;

cascadedMessageExpression:
	messageExpression (
		'.' (
			unarySelector
			| binarySelector unaryObjectDescription
			| (keyword binaryObjectDescription)+
		)
	)+;

expression: (variableName ASSIGN_ARROW)* (
		primary
		| messageExpression
		| cascadedMessageExpression
	);

// Zero or more statements, each either "! expression" or "expression", separated by '.'
statements: ( statement ( '.' statement)*)?;

statement: '!' expression | expression;

block:
	'[' statements? ']'
	| '[' (':' variableName)+ '|' statements ']';

temporaries: '|' variableName* '|';

// A complete method: a messagePattern header plus optional temporaries/statements
method:
	messagePattern
	| messagePattern temporaries statements
	| messagePattern statements;

// Method header: unary, binary(name), or keyword:name segments
messagePattern:
	unarySelector
	| binarySelector variableName
	| (keyword variableName)+;

//----------------- Lexer rules -----------------

ASSIGN_ARROW: '<-' | '←';

NUMBER: (DIGITS 'r')? '-'? DIGITS ('.' DIGITS)? ('e' '-'? DIGITS)?;
IDENTIFIER: LETTER ( LETTER | DIGIT)*;

STRING: '\'' ( CHARACTER | '\'\'') '\'';

COMMENT: '"' ( CHARACTER | '""' | '\'')* '"' -> skip;

WS: [ \t\r\n]+ -> skip;