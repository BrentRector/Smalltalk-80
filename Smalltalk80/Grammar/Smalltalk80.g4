grammar Smalltalk80;

//////////////////////////////////////////////////////////////////////////
// Parser Entry Points
//////////////////////////////////////////////////////////////////////////

fileIn
  : chunk* EOF
  ;

replUnit
  : classDefinition BANG ws? EOF                     #ReplClassDef
  | methodCategoryHeader methodDefinition+ EOF       #ReplMethodsForCategory
  | classComment EOF                                 #ReplClassComment
  | sequence EOF                                     #ReplSequence
  ;

//////////////////////////////////////////////////////////////////////////
// File-Out Chunks
//////////////////////////////////////////////////////////////////////////

chunk
  : classDefinition ws? BANG ws?                     #ClassChunk
  | methodCategoryHeader methodDefinition+ ws? BANG ws? BANG ws? #MethodCategoryChunk
  | classComment ws? BANG ws?                        #ClassCommentChunk
  | COMMENT ws? BANG ws?                             #StandaloneCommentChunk
  | sequence ws? BANG ws?                            #ScriptChunk
  ;

//////////////////////////////////////////////////////////////////////////
// Whitespace & Comments
//////////////////////////////////////////////////////////////////////////

ws
  : (SEPARATOR | COMMENT)+
  ;

//////////////////////////////////////////////////////////////////////////
// Class Definitions & Comments
//////////////////////////////////////////////////////////////////////////

classDefinition
  : IDENTIFIER ws?
    ( 'subclass:'
    | 'variableSubclass:'
    | 'variableByteSubclass:'
    | 'variableWordSubclass:' )
    ws? HASH IDENTIFIER
    ( ws? classOption )*
    ws? BANG
  ;

classComment
  : className ws? ( 'class' ws? )? 'comment:' ws? stringLiteral
  ;

className
  : IDENTIFIER
  ;

classOption
  : 'instanceVariableNames:' ws? stringLiteral
  | 'classVariableNames:'    ws? stringLiteral
  | 'poolDictionaries:'      ws? stringLiteral
  | 'category:'              ws? stringLiteral
  ;

//////////////////////////////////////////////////////////////////////////
// Method Categories & Definitions
//////////////////////////////////////////////////////////////////////////

methodCategoryHeader
  : BANG ws? IDENTIFIER ws? ( 'class' ws? )? 'methodsFor:' ws? stringLiteral BANG ws?
  ;

methodDefinition
  : methodSelector ws? sequence ws? METHOD_END ws?
  ;


methodSelector
  : unarySelector
  | binarySelector ws? IDENTIFIER
  | keywordSelector
  ;

unarySelector
  : IDENTIFIER
  ;

binarySelector
  : BINARY_SELECTOR
  | MINUS
  ;

keywordSelector
  : keywordPart ( ws? keywordPart )*
  ;

keywordPart
  : KEYWORD ws? IDENTIFIER
  ;

//////////////////////////////////////////////////////////////////////////
// Sequences (REPL & Scripts)
//////////////////////////////////////////////////////////////////////////

sequence
  : temps? ws? statement ( ws? PERIOD ws? statement )* ws?
  ;

statement
  : answer
  | expression
  ;

temps
  : PIPE ws? IDENTIFIER ( ws? IDENTIFIER )* ws? PIPE
  ;

//////////////////////////////////////////////////////////////////////////
// Statements & Expressions
//////////////////////////////////////////////////////////////////////////

answer
  : CARROT ws? expression ws? PERIOD?
  ;

expressions
  : expression ( PERIOD ws? expression )*
  ;

expression
  : assignment
  | cascade
  | keywordSend
  | binarySend
  | primitive
  ;

cascade
  : ( keywordSend | binarySend ) ( ws? SEMI_COLON ws? message )+
  ;

assignment
  : variable ws? ASSIGNMENT ws? expression
  ;

message
  : binaryMessage
  | unaryMessage
  | keywordMessage
  ;

//////////////////////////////////////////////////////////////////////////
// Message Sends
//////////////////////////////////////////////////////////////////////////

keywordSend
  : binarySend keywordMessage
  ;

keywordMessage
  : ws? ( keywordPair ws? )+
  ;

keywordPair
  : KEYWORD ws? binarySend ws?
  ;

binarySend
  : unarySend binaryTail?
  ;

unarySend
  : operand ws? unaryTail?
  ;

unaryMessage
  : ws? unarySelector
  ;

binaryMessage
  : ws? ( BINARY_SELECTOR | MINUS ) ws? ( unarySend | operand )
  ;

unaryTail
  : unaryMessage ws? unaryTail? ws?
  ;

binaryTail
  : binaryMessage binaryTail?
  ;

//////////////////////////////////////////////////////////////////////////
// Operands & Literals
//////////////////////////////////////////////////////////////////////////

operand
  : literal
  | reference
  | subexpression
  ;

subexpression
  : OPEN_PAREN ws? expression ws? CLOSE_PAREN
  ;

variable
  : IDENTIFIER
  ;

reference
  : variable
  ;

literal
  : runtimeLiteral
  | parsetimeLiteral
  ;

runtimeLiteral
  : dynamicDictionary
  | dynamicArray
  | block
  ;

block
  : BLOCK_START BLOCK_END
  | BLOCK_START nonEmptyBlockBody BLOCK_END
  ;

nonEmptyBlockBody
  : blockParamList ( ws sequence )?
  | temps ws? sequence
  ;

blockParamList
  : ( ws? BLOCK_PARAM )+
  ;

dynamicDictionary
  : DYNDICT_START ws? expressions? ws? DYNARR_END
  ;

dynamicArray
  : DYNARR_START ws? expressions? ws? DYNARR_END
  ;

parsetimeLiteral
  : pseudoVariable
  | number
  | charConstant
  | literalArray
  | stringLiteral
  | symbol
  ;

number
  : numberExp
  | hex_
  | stFloat
  | stInteger
  | RADIX_INT
  ;

numberExp
  : ( stFloat | stInteger ) EXP stInteger
  ;

charConstant
  : CHARACTER_CONSTANT
  ;

hex_
  : MINUS? HEX HEXDIGIT+
  ;

stInteger
  : MINUS? DIGIT+
  ;

stFloat
  : MINUS? DIGIT+ PERIOD DIGIT+
  ;

pseudoVariable
  : RESERVED_WORD
  ;

stringLiteral
  : STRING
  ;

symbol
  : HASH bareSymbol
  ;

bareSymbol
  : IDENTIFIER
  | BINARY_SELECTOR
  | KEYWORD+
  | stringLiteral
  ;

literalArray
  : LITARR_START literalArrayRest
  ;

literalArrayRest
  : ws? ( parsetimeLiteral | bareLiteralArray | bareSymbol )* CLOSE_PAREN
  ;

bareLiteralArray
  : OPEN_PAREN literalArrayRest
  ;

primitive
  : LT ws? KEYWORD ws? DIGIT+ ws? GT
  ;

//////////////////////////////////////////////////////////////////////////
// Lexer Rules
//////////////////////////////////////////////////////////////////////////

SEPARATOR        : [ \t\r\n\u000C]+ ;
STRING           : '\'' ( ~['] | '\'\'')* '\'' ;
COMMENT          : '"' ( ~["] | '""')* '"' ;
BLOCK_START      : '[' ;
BLOCK_END        : ']' ;
OPEN_PAREN       : '(' ;
CLOSE_PAREN      : ')' ;
PIPE             : '|' ;
PERIOD           : '.' ;
SEMI_COLON       : ';' ;
METHOD_END       : '!!' ;
BANG             : '!' ;
MINUS            : '-' ;
BINARY_SELECTOR  : ( '\\' | '+' | '*' | '/' | '=' | '>' | '<' | ',' 
                   | '@' | '%' | '~' | '|' | '&' | '?' )+ ;
LT               : '<' ;
GT               : '>' ;
CARROT           : '^' ;
HASH             : '#' ;
EXP              : 'e' ;
HEX              : '16r' ;
LITARR_START     : '#(' ;
DYNDICT_START    : '#{' ;
DYNARR_END       : '}' ;
DYNARR_START     : '{' ;
RESERVED_WORD    : 'nil' | 'true' | 'false' | 'self' | 'super' ;
IDENTIFIER       : [a-zA-Z] [a-zA-Z0-9_]* ;
CHARACTER_CONSTANT : '$' ~[\r\n] ;
DIGIT            : [0-9] ;
HEXDIGIT         : [0-9a-fA-F] ;
RADIX_INT        : '-'? DIGIT+ 'r' [0-9A-Za-z]+ ;
KEYWORD          : IDENTIFIER COLON ;
BLOCK_PARAM      : COLON IDENTIFIER ;
COLON            : ':' ;
ASSIGNMENT       : ':=' | '_' ;
DOLLAR           : '$' ;
