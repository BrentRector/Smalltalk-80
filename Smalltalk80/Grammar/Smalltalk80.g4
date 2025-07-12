grammar Smalltalk80;

// ───────────────────────────────────────────────────────────────
// Entry Points
// ───────────────────────────────────────────────────────────────

fileIn: chunk* EOF;                                // file-out style with ! terminators
expressionEntry: expression EOF;                   // REPL expression input
statementsEntry: statements EOF;                   // Workspace or do-it block
methodEntry: methodHeader temporaries? primitiveDecl? methodBodyNoBang EOF;
classEntry: classHeader classBody EOF;             // Class editor input

// ───────────────────────────────────────────────────────────────
// Chunks (file-in mode)
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

// ───────────────────────────────────────────────────────────────
// Comments & Metadata
// ───────────────────────────────────────────────────────────────

classCommentChunk:
    className 'comment:' NEW_LINE? stringConst BANG;

metaclassDefinitionChunk:
    className 'class' classSlotSpec* BANG;

metaclassCommentChunk:
    className 'class' 'comment:' NEW_LINE? stringConst BANG;

// ───────────────────────────────────────────────────────────────
// Method Definitions
// ───────────────────────────────────────────────────────────────

methodDef:
    methodHeader stringConst? NEW_LINE? temporaries? primitiveDecl? methodBody;

methodBody:
    statements PERIOD* BANG       // standard method
  | returnExpr BANG               // short form return
  | BANG;                         // empty method

methodBodyNoBang:
    statements PERIOD*            // REPL-compatible
  | returnExpr;                   // ^expr (no bang)

methodCategoryChunk:
    BANG className ('class')? 'methodsFor:' stringConst BANG (methodDef)* BANG;

methodHeader:
    unarySelector
  | binarySelector formalArg
  | (IDENTIFIER COLON formalArg)+;

formalArg: IDENTIFIER;

temporaries: PIPE IDENTIFIER+ PIPE NEW_LINE?;

// ───────────────────────────────────────────────────────────────
// Statements & Expressions
// ───────────────────────────────────────────────────────────────

statements: statement (PERIOD+ statement)* PERIOD*;

statement: answer | expression;

answer: returnExpr;

returnExpr:
    CARAT expression (SEMICOLON cascadeMessage)* PERIOD?;

// ───────────────────────────────────────────────────────────────
// Expression Parsing with Operator Precedence
// ───────────────────────────────────────────────────────────────

expression: lowPrecedenceExpr | messageExpr | assignmentExpr;

assignmentExpr: lowPrecedenceExpr ASSIGN expression;

lowPrecedenceExpr:
    mediumPrecedenceExpr ((PLUS | MINUS) mediumPrecedenceExpr)*;

mediumPrecedenceExpr:
    highPrecedenceExpr ((STAR | DIVIDE | MOD) highPrecedenceExpr)*;

highPrecedenceExpr:
    unaryMessageExpr
  | primaryExpr;

unaryMessageExpr: unarySelector primaryExpr;

messageExpr:
    primaryExpr (
        binarySelector expression
      | keywordMessage expression
    )*;

primaryExpr: literal | variable | '(' expression ')';

// ───────────────────────────────────────────────────────────────
// Message Parts
// ───────────────────────────────────────────────────────────────

unaryMessage: unarySelector primaryExpr;
binaryMessage: binarySelector expression;
keywordMessage: keywordPart+;

keywordPart: IDENTIFIER COLON expression;

// ───────────────────────────────────────────────────────────────
// Selectors
// ───────────────────────────────────────────────────────────────

unarySelector: '!' | '-' | '~';

binarySelector:
    STAR | DIVIDE | MOD | PLUS | MINUS | LT | GT | EQUAL | LEQ | GEQ
  | AT | QUESTION | BACKSLASH | COMMA | AND | OR | TILDE;

// ───────────────────────────────────────────────────────────────
// Cascade Messages
// ───────────────────────────────────────────────────────────────

cascadeMessage: expression (SEMICOLON expression)*;

// ───────────────────────────────────────────────────────────────
// Literals & Variables
// ───────────────────────────────────────────────────────────────

primary: literal | variable | block | '(' expression ')';

variable: IDENTIFIER | SELF | SUPER | TRUE | FALSE | NIL;

block: '[' blockArgs? blockTemps? statements? ']';
blockArgs: (COLON IDENTIFIER)+ PIPE?;
blockTemps: PIPE IDENTIFIER+ PIPE;

literal:
    stringConst
  | numberLiteral
  | symbolConst
  | arrayLiteral
  | byteArrayLiteral
  | characterLiteral
  | TRUE | FALSE | NIL;

numberLiteral: NUMBER;

stringConst: STRING;
characterLiteral: CHAR_LITERAL;
byteArrayLiteral: HASH '[' NUMBER* ']';

// ───────────────────────────────────────────────────────────────
// Arrays & Symbols
// ───────────────────────────────────────────────────────────────

arrayLiteral: HASH '(' arrayElement* ')';

arrayElement: literal | symbolName;

symbolConst: QUOTED_SYMBOL | HASH (symbolName);

symbolName:
    IDENTIFIER
  | unarySelector
  | binarySelector
  | keywordSymbolSelector;

keywordSymbolSelector: (IDENTIFIER COLON)+;

// ───────────────────────────────────────────────────────────────
// Other Chunks
// ───────────────────────────────────────────────────────────────

separatorChunk: '--' (~BANG)* BANG;

// ───────────────────────────────────────────────────────────────
// Lexer Rules
// ───────────────────────────────────────────────────────────────

className: IDENTIFIER;

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

PRIMITIVE_DECL: '<' WS_INLINE* 'primitive:' WS_INLINE* DIGITS WS_INLINE* '>';

NUMBER:
    DIGITS [rR] BASEDIGIT+
  | DIGITS ('.' DIGITS)? (('e' | 'E') ('+' | '-')? DIGITS)?;

COMMENT: '"' ('""' | ~'"')* '"' -> channel(HIDDEN);

STRING: '\'' ( '\'\'' | .)*? '\'' | '"' ( '""' | .)*? '"';

QUOTED_SYMBOL: HASH '\'' ( '\'\'' | ~'\'')* '\'';
CHAR_LITERAL: '$' ~[\r\n];

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
