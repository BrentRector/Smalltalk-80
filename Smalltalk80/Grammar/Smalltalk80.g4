grammar Smalltalk80;

// Parser Entry Point: a sequence of file-out chunks, each ending with '!'
fileIn: chunk* EOF;

// A chunk is a class definition, a method category with methods, or arbitrary code
chunk:
        classDefinition BANG ws?
        | methodCategoryHeader methodDefinition+
        | classComment
        | commentChunk
        | sequence BANG ws?;

commentChunk: ws? COMMENT (BANG ws?)?;

// Class definition chunk: Object subclass: #MyClass … category: 'Cat' !
classDefinition:
	IDENTIFIER ws? (
		'subclass:'
		| 'variableSubclass:'
		| 'variableByteSubclass:'
		| 'variableWordSubclass:'
	) ws? HASH IDENTIFIER (ws? classOption)*;

classComment:
	className ws? ('class' ws?)? 'comment:' ws? stringLiteral BANG ws?;

className: IDENTIFIER;

classOption:
	'instanceVariableNames:' ws? stringLiteral
	| 'classVariableNames:' ws? stringLiteral
	| 'poolDictionaries:' ws? stringLiteral
	| 'category:' ws? stringLiteral;

// Method-category header: !MyClass methodsFor: 'accessing'!
methodCategoryHeader:
	BANG ws? IDENTIFIER ws? ('class' ws?)? // optional literal class
	'methodsFor:' ws? stringLiteral BANG ws?;

// A single method under the current category with full selector
methodDefinition:
        methodSelector ws? sequence (METHOD_END | BANG) ws?;

methodSelector:
        unarySelector
        | binarySelector ws? IDENTIFIER
        | keywordSelector;

keywordSelector: keywordPart (ws? keywordPart)*;
keywordPart: KEYWORD ws? IDENTIFIER;

binarySelector: BINARY_SELECTOR | MINUS;

// Optional standalone script entry point
script: sequence EOF;

// Sequence of temporaries and statements
sequence: temps? ws? statements?;
// Temporaries can be surrounded by whitespace to match typical Smalltalk
// formatting such as "| x y |" or "|x y|".
temps: PIPE ws? IDENTIFIER (ws? IDENTIFIER)* ws? PIPE;

// Whitespace and comments
ws: (SEPARATOR | COMMENT)+;

// Statements: answer or expressions, possibly with trailing period
statements:
	answer ws?							# StatementAnswer
	| expressions ws? PERIOD ws? answer	# StatementExpressionsAnswer
	| expressions PERIOD? ws?			# StatementExpressions;

// Return: ^expr
answer: CARROT ws? expression ws? PERIOD?;

// One or more expressions
expressions: expression expressionList*;

expressionList: PERIOD ws? expression;

// Core expression forms
expression:
	assignment
	| cascade
	| keywordSend
	| binarySend
	| primitive;

// Cascaded messages: obj msg1; msg2; ...
cascade: (keywordSend | binarySend) (ws? SEMI_COLON ws? message)+;

// Assignment: var := expr
assignment: variable ws? ASSIGNMENT ws? expression;

variable: IDENTIFIER;

// Message sends
keywordSend: binarySend keywordMessage;

keywordMessage: ws? (keywordPair ws?)+;

keywordPair: KEYWORD ws? binarySend ws?;

binarySend: unarySend binaryTail?;

unarySend: operand ws? unaryTail?;

// Combine all message forms
message: binaryMessage | unaryMessage | keywordMessage;

// Operands: literals, variables, parenthesized expressions
operand: literal | reference | subexpression;

subexpression: OPEN_PAREN ws? expression ws? CLOSE_PAREN;

// Literals: runtime or parse-time
literal: runtimeLiteral | parsetimeLiteral;

runtimeLiteral: dynamicDictionary | dynamicArray | block;

block: BLOCK_START blockParamList? ws? sequence? BLOCK_END;

blockParamList: (ws? BLOCK_PARAM)+;

dynamicDictionary:
	DYNDICT_START ws? expressions? ws? DYNARR_END;

dynamicArray: DYNARR_START ws? expressions? ws? DYNARR_END;

parsetimeLiteral:
	pseudoVariable
	| number
	| charConstant
	| literalArray
	| stringLiteral
	| symbol;

number: numberExp | hex_ | stFloat | stInteger | RADIX_INT;

numberExp: (stFloat | stInteger) EXP stInteger;

charConstant: CHARACTER_CONSTANT;

hex_: MINUS? HEX HEXDIGIT+;

stInteger: MINUS? DIGIT+;

stFloat: MINUS? DIGIT+ PERIOD DIGIT+;

pseudoVariable: RESERVED_WORD;

stringLiteral: STRING;

symbol: HASH bareSymbol;

primitive: LT ws? KEYWORD ws? DIGIT+ ws? GT;

bareSymbol: (IDENTIFIER | BINARY_SELECTOR)
	| KEYWORD+
	| stringLiteral;

literalArray: LITARR_START literalArrayRest;

literalArrayRest:
	ws? ((parsetimeLiteral | bareLiteralArray | bareSymbol) ws?)* CLOSE_PAREN;

bareLiteralArray: OPEN_PAREN literalArrayRest;

unaryTail: unaryMessage ws? unaryTail? ws?;

unaryMessage: ws? unarySelector;

unarySelector: IDENTIFIER;

reference: variable;

binaryTail: binaryMessage binaryTail?;

binaryMessage:
	ws? (BINARY_SELECTOR | MINUS) ws? (unarySend | operand);

// —————————————————————————————————————————— Lexer Rules ——————————————————————————————————————————

SEPARATOR: [ \t\r\n\u000C]+;

STRING: '\'' ( ~['] | '\'\'')* '\'';
COMMENT: '"' ( ~["] | '""')* '"';

BLOCK_START: '[';
BLOCK_END: ']';
OPEN_PAREN: '(';
CLOSE_PAREN: ')';
PIPE: '|';
PERIOD: '.';
SEMI_COLON: ';';
METHOD_END: '!!';
BANG: '!';
MINUS: '-';

BINARY_SELECTOR: (
		'\\'
		| '+'
		| '*'
		| '/'
		| '='
		| '>'
		| '<'
		| ','
		| '@'
		| '%'
		| '~'
		| '|'
		| '&'
		| '?'
	)+;

LT: '<';
GT: '>';
CARROT: '^';
HASH: '#';
EXP: 'e';
HEX: '16r';
LITARR_START: '#(';
DYNDICT_START: '#{';
DYNARR_END: '}';
DYNARR_START: '{';
METHOD_END: '!!';

RESERVED_WORD: 'nil' | 'true' | 'false' | 'self' | 'super';

IDENTIFIER: [a-zA-Z]+ [a-zA-Z0-9_]*;

CHARACTER_CONSTANT: '$' ~[\r\n];

DIGIT: [0-9];

HEXDIGIT: [0-9a-fA-F];
RADIX_INT: '-'? DIGIT+ 'r' [0-9A-Za-z]+;

KEYWORD: IDENTIFIER COLON;

BLOCK_PARAM: COLON IDENTIFIER;

COLON: ':';

// after
ASSIGNMENT: ':=' | '_';

DOLLAR: '$';