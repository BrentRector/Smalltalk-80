grammar Smalltalk80;

// ───────────────────────────────────────────
//  Entry point
// ───────────────────────────────────────────
fileIn : chunk* EOF ;

// ───────────────────────────────────────────
//  Chunks (classic file‑out units)
// ───────────────────────────────────────────
chunk
    : classDefChunk
    | classCommentChunk
    | methodCategoryChunk
    | separatorChunk
    ;

// ── Class definition chunk ────────────────
classDefChunk : classHeader classBody BANG ;

classHeader : className subclassKind symbolConst ;

subclassKind
    : 'subclass:'
    | 'variableSubclass:'
    | 'variableByteSubclass:'
    | 'variableWordSubclass:'
    ;

classBody : classSlotSpec* categoryDecl ;

classSlotSpec
    : 'instanceVariableNames:'        stringConst
    | 'classVariableNames:'           stringConst
    | 'classInstanceVariableNames:'   stringConst
    | 'poolDictionaries:'             stringConst
    ;

categoryDecl : 'category:' stringConst ;

// ── Class comment chunk ────────────────────
classCommentChunk : BANG className 'comment:' NEW_LINE? stringConst BANG ;

// ── Method category chunk ──────────────────
methodCategoryChunk
    : BANG (className ('class')?) 'methodsFor:' stringConst BANG
      methodDef*
      BANG
    ;

// ───────────────────────────────────────────
//  Method definition
// ───────────────────────────────────────────
methodDef
    : methodHeader NEW_LINE?
      temporaries?
      primitiveDecl?
      statements?
      BANG
    ;

methodHeader
    : unarySelector
    | binarySelector formalArg
    | keywordSelector formalArg+
    ;

formalArg   : IDENTIFIER ;

temporaries : PIPE IDENTIFIER+ PIPE NEW_LINE? ;

primitiveDecl : PRIMITIVE_DECL ;

// ───────────────────────────────────────────
//  Statements and expressions
// ───────────────────────────────────────────
answer     : returnExpr PERIOD? ;
statement  : answer | expression ;
statements : statement (PERIOD statement)* PERIOD? ;

expression
    : assignmentExpression (SEMICOLON cascadeMessage)*
    ;

returnExpr
    : CARAT assignmentExpression (SEMICOLON cascadeMessage)*
    ;

assignmentExpression
    : variable ASSIGN expression
    | keywordExpression
    ;

cascadeMessage : messageSend ;

messageSend
    : unaryMessage
    | binaryMessage
    | keywordMessage
    ;

keywordExpression : binaryExpression (keywordMessage)* ;

keywordMessage
    : keywordSelector binaryExpression (keywordSelector binaryExpression)*
    ;

binaryExpression : unaryExpression (binaryMessage)* ;

binaryMessage : binarySelector unaryExpression ;

unaryExpression : primary (unarySelector)* ;

unaryMessage : unarySelector ;

// ───────────────────────────────────────────
//  Primary expressions
// ───────────────────────────────────────────
primary
    : MINUS numberLiteral
    | literal
    | variable
    | block
    | '(' expression ')'
    ;

variable : IDENTIFIER ;

// ───────────────────────────────────────────
//  Blocks
// ───────────────────────────────────────────
block        : '[' blockArgs? blockTemps? statements? ']' ;
blockArgs    : (COLON IDENTIFIER)+ PIPE? ;
blockTemps   : PIPE IDENTIFIER+ PIPE ;

// ───────────────────────────────────────────
//  Literals
// ───────────────────────────────────────────
literal
    : stringConst
    | numberLiteral
    | symbolConst
    | arrayLiteral
    | byteArrayLiteral
    | characterLiteral
    | trueLiteral
    | falseLiteral
    | nilLiteral
    ;

numberLiteral : UNSIGNED_NUMBER ;

byteArrayLiteral : HASH '[' UNSIGNED_NUMBER* ']' ;

symbolConst
    : QUOTED_SYMBOL
    | HASH ( MINUS | keywordSelector | unarySelector | binarySelector )
    ;

// Localize array elements to literals or bare symbol names
arrayLiteral
    : HASH '(' arrayElement* ')'
    ;

arrayElement
    : literal
    | symbolName        // bare identifier or selector turned into symbol
    ;

// symbolName matches Smalltalk symbol tokens without '#'
symbolName
    : IDENTIFIER
    | unarySelector
    | binarySelector
    | keywordSelector
    ;

characterLiteral : CHAR_LITERAL ;

trueLiteral  : 'true' ;
falseLiteral : 'false' ;
nilLiteral   : 'nil'  ;

// ───────────────────────────────────────────
//  Separator chunk
// ───────────────────────────────────────────
separatorChunk : '--' (~BANG)* BANG ;

// ───────────────────────────────────────────
//  Helpers & selectors
// ───────────────────────────────────────────
className   : IDENTIFIER ;
stringConst : STRING ;

unarySelector   : IDENTIFIER ;
binarySelector  : MINUS | BINARY_SELECTOR ;
keywordSelector : (IDENTIFIER COLON)+ ;

// ───────────────────────────────────────────
//  Lexer rules
// ───────────────────────────────────────────
BANG      : '!' ;
COLON     : ':' ;
PERIOD    : '.' ;
ASSIGN    : ':=' | '_' ;
CARAT     : '^' ;
PIPE      : '|' ;
SEMICOLON : ';' ;
HASH      : '#' ;
MINUS     : '-' ;

PRIMITIVE_DECL : '<' 'primitive:' DIGITS '>' ;

UNSIGNED_NUMBER
    : DIGITS [rR] BASEDIGIT+
    | DIGITS ('.' DIGITS)? (('e'|'E') ('+'|'-')? DIGITS)?
    ;

STRING        : '\'' ( '\'\'' | ~'\'' )* '\'' ;
QUOTED_SYMBOL : HASH '\'' ( '\'\'' | ~'\'' )* '\'' ;
CHAR_LITERAL  : '$' ~[\r\n] ;

BINARY_SELECTOR
    : ([+*/%~<>=@|&!?\\,]|'-') ([+*/%~<>=@|&!?\\,]|'-')*
    ;

IDENTIFIER     : LETTER (LETTER|DIGIT)* ;
fragment LETTER : [A-Za-z] ;
fragment DIGIT  : [0-9] ;
fragment BASEDIGIT : [0-9A-Za-z] ;
DIGITS         : DIGIT+ ;

NEW_LINE : ('\r'? '\n')+ -> skip ;
WS       : [ \t\f]+      -> skip ;
COMMENT  : '"' .*? '"'   -> skip ;
