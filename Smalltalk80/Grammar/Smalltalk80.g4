grammar Smalltalk80;

// ─────────────────────────────────────────────────────────────── Entry Points
// ───────────────────────────────────────────────────────────────

fileIn: chunk* EOF; // file-out style with ! terminators
expressionEntry: expression EOF; // REPL expression input
statementsEntry: statements EOF; // Workspace or do-it block
methodEntry:
	methodHeader temporaries? primitiveDecl? methodBodyNoBang EOF;
classEntry: classHeader classBody EOF; // Class editor input

// ─────────────────────────────────────────────────────────────── Chunks (file-in mode)
// ───────────────────────────────────────────────────────────────

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

// ─────────────────────────────────────────────────────────────── Comments & Metadata
// ───────────────────────────────────────────────────────────────

classCommentChunk:
	className 'comment:' NEW_LINE? stringConst BANG;

metaclassDefinitionChunk: className CLASS classSlotSpec* BANG;

metaclassCommentChunk:
	className CLASS 'comment:' NEW_LINE? stringConst BANG;

// ─────────────────────────────────────────────────────────────── Method Definitions
// ───────────────────────────────────────────────────────────────

methodDef:
	methodHeader stringConst? NEW_LINE? temporaries? primitiveDecl? methodBody;

methodBody:
	statements PERIOD* BANG // standard method
	| returnExpr BANG // short form return
	| BANG; // empty method

methodBodyNoBang:
	statements PERIOD* // REPL-compatible
	| returnExpr; // ^expr (no bang)

methodCategoryChunk:
	BANG className (CLASS)? 'methodsFor:' stringConst BANG (
		methodDef
	)* BANG;

methodHeader:
	unarySelector
	| binarySelector formalArg
	| (IDENTIFIER COLON formalArg)+;

formalArg: argumentName;

temporaries: PIPE temporaryName+ PIPE NEW_LINE?;
temporaryName: IDENTIFIER | CLASS;
// ─────────────────────────────────────────────────────────────── Statements & Expressions
// ───────────────────────────────────────────────────────────────

statements: statement (PERIOD+ statement)* PERIOD*;

//🔧 statement: handles return and expression only
statement: answer | expression;

answer: returnExpr;

returnExpr: CARAT expression PERIOD?;

// ─────────────────────────────────────────────────────────────── Expression Parsing with Operator
// Precedence ───────────────────────────────────────────────────────────────

// Expressions may be simple arithmetic, message sends, assignments, or a return expression used by
// do-it blocks and workspace evaluations.

// 🔧 expression: full expression, possibly with cascade
expression: baseExpression (SEMICOLON messagePart)*;

// 🔧 baseExpression: no cascade, just real expressions
baseExpression:
	returnExpr
	| assignmentExpr
	| messageExpr
	| lowPrecedenceExpr;

messagePart: unaryMessage | binaryMessage | keywordMessage;

assignmentExpr: lowPrecedenceExpr ASSIGN expression;

lowPrecedenceExpr:
	mediumPrecedenceExpr ((PLUS | MINUS) mediumPrecedenceExpr)*;

mediumPrecedenceExpr:
	highPrecedenceExpr ((STAR | DIVIDE | MOD) highPrecedenceExpr)*;

highPrecedenceExpr: primaryExpr | prefixExpr;

prefixExpr: (MINUS | TILDE) primaryExpr;

messageBase:
	parenthesizedExpr
	| returnExpr
	| assignmentExpr
	| lowPrecedenceExpr;

// A message expression consists of a primary receiver followed by zero or more unary, binary, or
// keyword messages. This aligns with the full Smalltalk message syntax while remaining simple for
// this prototype grammar.
messageExpr:
	messageBase (unaryMessage | binaryMessage | keywordMessage)+
	| messageBase;

parenthesizedExpr: '(' expression ')';

primaryExpr: literal | variable | block | '(' expression ')';

// ─────────────────────────────────────────────────────────────── Message Parts
// ───────────────────────────────────────────────────────────────

// Unary messages have no arguments and simply apply a selector to the receiver.
unaryMessage: unarySelector;
binaryMessage: binarySelector expression;
keywordMessage: keywordPart+;

keywordPart: IDENTIFIER COLON expression;

// ─────────────────────────────────────────────────────────────── Selectors
// ───────────────────────────────────────────────────────────────

// A unary selector is either an identifier (e.g., foo) or one of the special unary messages used by
// the standard library. Supporting identifiers here allows the grammar to parse regular unary
// method definitions and sends such as `increment` or `yourself`.
unarySelector:
	IDENTIFIER
	| CLASS
	| SELF
	| SUPER
	| TRUE
	| FALSE
	| NIL;

binarySelector:
	// ───────────── Arithmetic Operators ─────────────
	PLUS // +
	| MINUS // -
	| STAR // *
	| DIVIDE // /
	| MOD // %
	| INTEGER_DIVIDE // //
	| MODULO // \\

	// ───────────── Bitwise Operators ─────────────
        | BIT_SHIFT_LEFT // <<
        | BIT_SHIFT_RIGHT // >>
        | BACKSLASH // \
        | TILDE // ~
        | PIPE // |
        | BIT_AND // &

	// ───────────── Comparison Operators ─────────────
	| EQUAL // =
	| NOT_EQUAL // ~=
	| EQUAL_EQUAL // ==
	| NOT_IDENTITY // ~~
	| LT // <
	| LEQ // <=
	| GT // >
	| GEQ // >=

	// ───────────── Other Symbols & Messaging ─────────────
	| AT // @
	| QUESTION // ?
	| COMMA // ,
	| AND // and:
	| OR; // or:

primary: literal | variable | block | '(' expression ')';

variable:
	IDENTIFIER
	| CLASS
	| SELF
	| SUPER
	| TRUE
	| FALSE
	| NIL;

block: '[' blockArgs? blockTemps? statements? ']';

blockArgs: (COLON argumentName)+ PIPE?;
argumentName: IDENTIFIER | CLASS;

blockTemps: PIPE temporaryName+ PIPE;

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

// ─────────────────────────────────────────────────────────────── Arrays & Symbols
// ───────────────────────────────────────────────────────────────

arrayLiteral: HASH '(' arrayElement* ')';

arrayElement: literal | symbolName;

symbolConst: QUOTED_SYMBOL | HASH (symbolName);

symbolName:
	IDENTIFIER
	| CLASS
	| SELF
	| SUPER
	| TRUE
	| FALSE
	| NIL
	| unarySelector
	| binarySelector
	| keywordSymbolSelector;

keywordSymbolSelector: (IDENTIFIER COLON)+;

// ─────────────────────────────────────────────────────────────── Other Chunks
// ───────────────────────────────────────────────────────────────

separatorChunk: '--' (~BANG)* BANG;
className: IDENTIFIER;

// ─────────────────────────────────────────────────────────────── Lexer Rules
// ───────────────────────────────────────────────────────────────

CLASS: 'class';

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

PRIMITIVE_DECL:
	'<' WS_INLINE* 'primitive:' WS_INLINE* DIGITS WS_INLINE* '>';

MINUS: '-';
TILDE: '~';

NUMBER:
	DIGITS [rR] BASEDIGIT+ // radix
	| DIGITS ('.' DIGITS)? (('e' | 'E') ('+' | '-')? DIGITS)?; // decimal

COMMENT: '"' ('""' | ~'"')* '"' -> channel(HIDDEN);

STRING: '\'' ( '\'\'' | ~'\'')* '\''; // single-quote strings

QUOTED_SYMBOL: HASH '\'' ( '\'\'' | ~'\'')* '\'';
CHAR_LITERAL: '$' ~[\r\n];

BIT_SHIFT_LEFT: '<<';
BIT_SHIFT_RIGHT: '>>';
MODULO: '\\\\';
INTEGER_DIVIDE: '//';
EQUAL_EQUAL: '==';
NOT_EQUAL: '~=';
NOT_IDENTITY: '~~';
LEQ: '<=';
GEQ: '>=';
PLUS: '+';
STAR: '*';
DIVIDE: '/';
MOD: '%';
LT: '<';
GT: '>';
EQUAL: '=';
AT: '@';
QUESTION: '?';
BACKSLASH: '\\';
COMMA: ',';
BIT_AND: '&';
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