grammar Smalltalk80;

/*
 ───────────────────────────────────────────────────────────
 Important Contextual Distinction
 ───────────────────────────────────────────────────────────
 | Use case | Rule used |
 |------------------------------|-----------------------------------|
 | Message send (e.g., `a at:
 i`) | `keywordMessage` with arguments |
 | Method header (e.g., `at:put:`) | `(IDENTIFIER COLON
 formalArg)+` |
 | Symbol (e.g., `#at:put:`) | `keywordSymbolSelector` |
 */

// ─────────────────────────────────────────── Entry point
fileIn: chunk* EOF;

// ─────────────────────────────────────────── Chunks
chunk:
	metaclassCommentChunk
	| metaclassDefinitionChunk
	| classCommentChunk
	| classDefChunk
	| methodCategoryChunk
	| separatorChunk
	| BANG;

// ─────────────────────────────────────────── Class definitions
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

// ─────────────────────────────────────────── Class & metaclass comments
classCommentChunk:
	className 'comment:' NEW_LINE? stringConst BANG;

metaclassDefinitionChunk: className 'class' classSlotSpec* BANG;

metaclassCommentChunk:
	className 'class' 'comment:' NEW_LINE? stringConst BANG;

// ─────────────────────────────────────────── Method category
methodCategoryChunk:
	BANG (className ('class')?) 'methodsFor:' stringConst BANG methodDef* BANG;

// ─────────────────────────────────────────── Method definition
methodDef:
	methodHeader NEW_LINE? temporaries? primitiveDecl? statements? BANG;

methodHeader:
	unarySelector
	| binarySelector formalArg
	| (IDENTIFIER COLON formalArg)+;

formalArg: IDENTIFIER;
temporaries: PIPE IDENTIFIER+ PIPE NEW_LINE?;

// ─────────────────────────────────────────── Statements and expressions
statements: statement (PERIOD statement)* PERIOD?;
statement: answer | expression;
answer: returnExpr PERIOD?;

expression:
	assignable ASSIGN expression	# assignmentExpr
	| keywordSend					# messageExpr;

assignable: variable;

keywordSend: binarySend keywordMessage*;

binarySend: unarySend (binaryMessage)*;

unarySend : primary (unaryMessage)*;

returnExpr: CARAT expression (SEMICOLON cascadeMessage)*;

assignmentExpression:
	variable ASSIGN expression
	| keywordExpression;

keywordExpression: binaryExpression keywordMessage?;

cascadeMessage: messageSend;
messageSend: unaryMessage | binaryMessage | keywordMessage;

unaryMessage: unarySelector;
binaryMessage: binarySelector unaryExpression;
binaryExpression: unaryExpression (binaryMessage)*;
unaryExpression: primary (unarySelector)*;

// ─────────────────────────────────────────── Keyword messages
keywordMessage: keywordPart+;
keywordPart: IDENTIFIER COLON expression;

// ─────────────────────────────────────────── Primary expressions
primary:
    literal
	| variable
	| block
	| '(' expression ')';


variable: IDENTIFIER;

// ─────────────────────────────────────────── Blocks
block: '[' blockArgs? blockTemps? statements? ']';
blockArgs: (COLON IDENTIFIER)+ PIPE?;
blockTemps: PIPE IDENTIFIER+ PIPE;

// ─────────────────────────────────────────── Literals
literal:
	stringConst
	| numberLiteral
	| symbolConst
	| arrayLiteral
	| byteArrayLiteral
	| characterLiteral
	| trueLiteral
	| falseLiteral
	| nilLiteral;

numberLiteral: NUMBER;

stringConst: STRING;
characterLiteral: CHAR_LITERAL;
trueLiteral: 'true';
falseLiteral: 'false';
nilLiteral: 'nil';

byteArrayLiteral: HASH '[' NUMBER* ']';

// ─────────────────────────────────────────── Arrays and symbols
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

// ─────────────────────────────────────────── Separator
separatorChunk: '--' (~BANG)* BANG;

// ─────────────────────────────────────────── Helper rules
className: IDENTIFIER;

// ─────────────────────────────────────────── Selectors
unarySelector: IDENTIFIER;
binarySelector: BINARY_SELECTOR;

// ─────────────────────────────────────────── Lexer rules
BANG: '!';
COLON: ':';
PERIOD: '.';
ASSIGN: ':=' | '_';
CARAT: '^';
PIPE: '|';
SEMICOLON: ';';
HASH: '#';

fragment INLINE_WS: [ \t];

PRIMITIVE_DECL: '<' INLINE_WS* 'primitive:' INLINE_WS* DIGITS INLINE_WS* '>';

NUMBER: ('+' | '-')? DIGITS [rR] BASEDIGIT+
      | ('+' | '-')? DIGITS ('.' DIGITS)? (('e' | 'E') ('+' | '-')? DIGITS)?;


STRING: '\'' ( '\'\'' | ~'\'')* '\'';
QUOTED_SYMBOL: HASH '\'' ( '\'\'' | ~'\'')* '\'';
CHAR_LITERAL: '$' ~[\r\n];

BINARY_SELECTOR: ([-+*/%~<>=@|&?\\,])+;

IDENTIFIER: LETTER (LETTER | DIGIT)*;

fragment LETTER: [A-Za-z];
fragment DIGIT: [0-9];
fragment BASEDIGIT: [0-9A-Za-z];
DIGITS: DIGIT+;

NEW_LINE: ('\r'? '\n')+ -> skip;
WS: [ \t\f]+ -> skip;
COMMENT: '"' .*? '"' -> skip;