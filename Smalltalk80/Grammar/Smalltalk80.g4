grammar Smalltalk80;

/*
 Important Contextual Distinction
 ─────────────────────────────────────────────────────────────────
 | Use case | Rule used |
 |----------------------------------|---------------------------------|
 | Message send (e.g., `a
 at: i`) | messageExpr (via message chain) |
 | Method header (e.g., `at:put:`) | (IDENTIFIER COLON
 formalArg)+ |
 | Symbol (e.g., `#at:put:`) | keywordSymbolSelector |
 */

// Entry point
fileIn: chunk* EOF;

// Chunks
chunk:
	metaclassCommentChunk
	| metaclassDefinitionChunk
	| classCommentChunk
	| classDefChunk
	| methodCategoryChunk
	| separatorChunk
	| expressionChunk
	| BANG;

expressionChunk: expression BANG;

// Class definitions
classDefChunk: classHeader classBody BANG;
classHeader: className subclassKind symbolConst;

subclassKind:
	'subclass:'
	| 'variableSubclass:'
	| 'variableByteSubclass:'
	| 'variableWordSubclass:';

primitiveDecl: PRIMITIVE_DECL;

classBody: classSlotSpec* categoryDecl;

classSlotSpec:
	'instanceVariableNames:' stringConst
	| 'classVariableNames:' stringConst
	| 'classInstanceVariableNames:' stringConst
	| 'poolDictionaries:' stringConst;

categoryDecl: 'category:' stringConst;

// Class & metaclass comments
classCommentChunk:
	className 'comment:' NEW_LINE? stringConst BANG;

metaclassDefinitionChunk: className 'class' classSlotSpec* BANG;

metaclassCommentChunk:
	className 'class' 'comment:' NEW_LINE? stringConst BANG;

// Method category
methodCategoryChunk:
	BANG (className ('class')?) 'methodsFor:' stringConst BANG methodDef* BANG;

// Method definition
methodDef:
	methodHeader stringConst? NEW_LINE? temporaries? primitiveDecl? statements? BANG;

methodHeader:
	unarySelector
	| binarySelector formalArg
	| (IDENTIFIER COLON formalArg)+;

formalArg: IDENTIFIER;
temporaries: PIPE IDENTIFIER+ PIPE NEW_LINE?;

// Statements and expressions
statements: statement (PERIOD+ statement)* PERIOD+;

statement: answer | expression;

answer: returnExpr;

returnExpr:
	CARAT expression (SEMICOLON cascadeMessage)* PERIOD?;

// Expression parsing with precedence handling for binary operations
expression: lowPrecedenceExpr | messageExpr | assignmentExpr;

// Assignment expression handling (expression := expression)
assignmentExpr: lowPrecedenceExpr ASSIGN expression;

// Expression handling binary operations with precedence
lowPrecedenceExpr:
	mediumPrecedenceExpr ((PLUS | MINUS) mediumPrecedenceExpr)*; // Low precedence operators (+, -)

mediumPrecedenceExpr:
	highPrecedenceExpr ((STAR | DIVIDE | MOD) highPrecedenceExpr)*;
// Medium precedence operators (*, /, %)

highPrecedenceExpr:
	unaryMessageExpr
	| primaryExpr; // High precedence includes unary messages

// Unary message expression
unaryMessageExpr: unarySelector primaryExpr; // Example: ~a, -b

// Message expression (handles both binary and keyword messages)
messageExpr:
	primaryExpr (
		binarySelector expression
		| keywordMessage expression
	)*;

// Primary expressions (handles literals, variables, etc.)
primaryExpr: literal | variable | '(' expression ')';

// Handles unary messages like ~a, -b
unaryMessage: unarySelector primaryExpr; // e.g., ~a, -b

// Handles binary messages like a + b, a * b
binaryMessage: binarySelector expression; // e.g., a + b, a * b

// Handles keyword messages like at:put:, at:
keywordMessage: keywordPart+; // e.g., at: 1, at:put: 2

// Keyword part handling (like at: or put:)
keywordPart: IDENTIFIER COLON expression;

// Selectors
unarySelector: '!' | '-' | '~'; // Example: !a, -a, ~a

binarySelector:
	STAR
	| DIVIDE
	| MOD
	| PLUS
	| MINUS
	| LT
	| GT
	| EQUAL
	| LEQ
	| GEQ
	| AT
	| QUESTION
	| BACKSLASH
	| COMMA
	| AND
	| OR
	| TILDE; // Example: a + b, a * b

assignable: variable;

cascadeMessage: expression (SEMICOLON expression)*;

// Primary expressions
primary: literal | variable | block | '(' expression ')';

variable: IDENTIFIER | SELF | SUPER | TRUE | FALSE | NIL;

// Blocks
block: '[' blockArgs? blockTemps? statements? ']';
blockArgs: (COLON IDENTIFIER)+ PIPE?;
blockTemps: PIPE IDENTIFIER+ PIPE;

// Literals
literal:
	stringConst
	| numberLiteral
	| symbolConst
	| arrayLiteral
	| byteArrayLiteral
	| characterLiteral
	| TRUE
	| FALSE
	| NIL;

numberLiteral: NUMBER;

stringConst: STRING;
characterLiteral: CHAR_LITERAL;
byteArrayLiteral: HASH '[' NUMBER* ']';

// Arrays and symbols
arrayLiteral: HASH '(' arrayElement* ')';

arrayElement: literal | symbolName;

symbolConst:
	QUOTED_SYMBOL
	| HASH (
		unarySelector
		| binarySelector
		| keywordSymbolSelector
	);

symbolName:
	IDENTIFIER
	| unarySelector
	| binarySelector
	| keywordSymbolSelector;

keywordSymbolSelector: (IDENTIFIER COLON)+;

// Separator
separatorChunk: '--' (~BANG)* BANG;

// Helper rules
className: IDENTIFIER;

// Lexer rules
BANG: '!';
COLON: ':';
PERIOD: '.';
ASSIGN: ':=' | '_';
CARAT: '^';
PIPE: '|';
SEMICOLON: ';';
HASH: '#';

SELF: 'self';
SUPER: 'super';
TRUE: 'true';
FALSE: 'false';
NIL: 'nil';

fragment INLINE_WS: [ \t\r\n\f];

PRIMITIVE_DECL:
	'<' WS_INLINE* 'primitive:' WS_INLINE* DIGITS WS_INLINE* '>';

NUMBER:
	DIGITS [rR] BASEDIGIT+
	| DIGITS ('.' DIGITS)? (('e' | 'E') ('+' | '-')? DIGITS)?;

COMMENT: '"' ('""' | ~'"')* '"' -> channel(HIDDEN);

STRING: '\'' ( '\'\'' | .)*? '\'' | '"' ( '""' | .)*? '"';

QUOTED_SYMBOL: HASH '\'' ( '\'\'' | ~'\'')* '\'';
CHAR_LITERAL: '$' ~[\r\n];

// Token definitions for binary operators
LEQ: '<=';
GEQ: '>=';
PLUS: '+';
MINUS: '-';
STAR: '*';
DIVIDE: '/';
MOD: '%';
TILDE: '~';
LT: '<';
GT: '>';
EQUAL: '=';
AT: '@';
QUESTION: '?';
BACKSLASH: '\\';
COMMA: ',';
AND: 'and:';
OR: 'or:';

IDENTIFIER: LETTER (LETTER | DIGIT)*;

fragment LETTER: [A-Za-z];
fragment DIGIT: [0-9];
fragment BASEDIGIT: [0-9A-Za-z];
fragment WS_INLINE: [ \t\f];
DIGITS: DIGIT+;

NEW_LINE: ('\r'? '\n')+ -> skip;

WS: [ \t\f]+ -> skip;