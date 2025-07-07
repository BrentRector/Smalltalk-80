grammar Smalltalk80;

//////////////////////////////////////////////////////////////////////////
// Parser Entry Points
//////////////////////////////////////////////////////////////////////////

fileIn
    : chunk* EOF
    ;

replUnit
    : classDefinition BANG EOF                         # ReplClassDef
    | methodCategoryHeader methodDefinition* EOF       # ReplMethodsForCategory
    | classComment EOF                                # ReplClassComment
    | sequence EOF                                    # ReplSequence
    ;

//////////////////////////////////////////////////////////////////////////
// File-In Chunks
//////////////////////////////////////////////////////////////////////////

chunk
    : /* COMMENT */ BANG                           		# StandaloneCommentChunk //COMMENT goes to hidden channel
    | classDefinition BANG                              # ClassChunk
    | classComment BANG                                 # ClassCommentChunk
    | metaclassDefinition BANG                          # MetaClassChunk
    | sequence BANG                                     # ScriptChunk
    | {
          InputStream.LA(2)==Smalltalk80Parser.IDENTIFIER &&
          (
            InputStream.LA(3)==Smalltalk80Parser.METHODS_FOR ||
            (InputStream.LA(3)==Smalltalk80Parser.CLASS &&
             InputStream.LA(4)==Smalltalk80Parser.METHODS_FOR)
          )
      }? methodCategoryHeader (methodDefinitionWithBang+ BANG)?  # MethodCategoryChunk
    ;

methodDefinitionWithBang
    : methodDefinition BANG
    ;

//////////////////////////////////////////////////////////////////////////
// Class & Metaclass Definitions + Comments
//////////////////////////////////////////////////////////////////////////

classDefinition
    : IDENTIFIER (
          'subclass:'
        | 'variableSubclass:'
        | 'variableByteSubclass:'
        | 'variableWordSubclass:'
      ) HASH IDENTIFIER (classOption)*
    ;

classComment
    : className CLASS? 'comment:' stringLiteral
    ;

className
    : IDENTIFIER
    ;

classOption
    : INSTANCE_VARIABLE_NAMES stringLiteral
    | CLASS_VARIABLE_NAMES    stringLiteral
    | POOL_DICTIONARIES       stringLiteral
    | CATEGORY                stringLiteral
    ;

metaclassDefinition
    : IDENTIFIER CLASS INSTANCE_VARIABLE_NAMES stringLiteral
    ;

//////////////////////////////////////////////////////////////////////////
// Method Categories & Definitions
//////////////////////////////////////////////////////////////////////////

methodCategoryHeader
    : BANG IDENTIFIER CLASS? METHODS_FOR stringLiteral BANG
    ;

methodDefinition
    : methodSelector primitive? sequence?
    ;

methodSelector
    : unarySelector
    | binarySelector IDENTIFIER
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
    : keywordPart+
    ;

keywordPart
    : KEYWORD IDENTIFIER
    ;

//////////////////////////////////////////////////////////////////////////
// Sequences (REPL & File-In Scripts)
//////////////////////////////////////////////////////////////////////////

sequence
    : temps? statements
    ;

statements
    : statement (PERIOD statement)* PERIOD?
    ;

statement
    : answer
    | expression
    ;

temps
    : PIPE IDENTIFIER+ PIPE
    ;

//////////////////////////////////////////////////////////////////////////
// Statements & Expressions
//////////////////////////////////////////////////////////////////////////

answer
    : CARROT expression PERIOD?
    ;

cascade
    : SEMI_COLON messageSend+
    ;

expressions
    : expression (PERIOD expression)*
    ;

expression
    : keywordExpr cascade*
    ;

keywordExpr
    : binaryExpr (KEYWORD expression)*
    ;

binaryExpr
    : unaryExpr (binaryMessage)*
    ;

unaryExpr
    : primaryExpression (unaryMessage)*
    ;

primaryExpression
    : assignment
    | primitive
    | operand
    ;

messageSend
    : unaryMessage
    | binaryMessage
    | (KEYWORD expression)+
    ;

unaryMessage
    : IDENTIFIER
	| CLASS
    ;

binaryMessage
    : (BINARY_SELECTOR | MINUS) unaryExpr
    ;

assignment
    : variable ASSIGNMENT expression
    ;

//////////////////////////////////////////////////////////////////////////
// Operands & Literals
//////////////////////////////////////////////////////////////////////////

operand
    : literal
    | reference
    | subexpression
    | block
    ;

subexpression
    : OPEN_PAREN expression CLOSE_PAREN
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
    : blockParamList PIPE temps? statements
    | blockParamList PIPE statements
    | temps? statements
    ;

blockParamList
    : BLOCK_PARAM+
    ;

dynamicDictionary
    : DYNDICT_START expressions? DYNARR_END
    ;

dynamicArray
    : DYNARR_START expressions? DYNARR_END
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
    : (stFloat | stInteger) EXP stInteger
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
    : (parsetimeLiteral | bareLiteralArray | bareSymbol)* CLOSE_PAREN
    ;

bareLiteralArray
    : OPEN_PAREN literalArrayRest
    ;

primitive
    : PRIMITIVE
    ;

//////////////////////////////////////////////////////////////////////////
// Lexer Rules
//////////////////////////////////////////////////////////////////////////

CLASS                   : 'class' ;
METHODS_FOR             : 'methodsFor:' ;
INSTANCE_VARIABLE_NAMES : 'instanceVariableNames:' ;
CLASS_VARIABLE_NAMES    : 'classVariableNames:' ;
POOL_DICTIONARIES       : 'poolDictionaries:' ;
CATEGORY                : 'category:' ;

// KEYWORD must appear before IDENTIFIER so that ifTrue: → KEYWORD
KEYWORD                 : [a-zA-Z] [a-zA-Z0-9_]* COLON ;
IDENTIFIER              : [a-zA-Z] [a-zA-Z0-9_]* ;

// whitespace
SEPARATOR               : [ \t\r\n\u000C]+    -> skip ;

// our one “visible” comment token to drive StandaloneCommentChunk:
COMMENT_BANG
    : '"' ( ~["] | '""' )* '"' '!'
    ;

// all other inline comments vanish into HIDDEN:
COMMENT
    : '"' ( ~["] | '""' )* '"'
    -> channel(HIDDEN)
    ;

STRING                  : '\'' ( ~['] | '\'\'')* '\'' ;
BLOCK_START             : '[' ;
BLOCK_END               : ']' ;
OPEN_PAREN              : '(' ;
CLOSE_PAREN             : ')' ;
PIPE                    : '|' ;
PERIOD                  : '.' ;
SEMI_COLON              : ';' ;
BANG                    : '!' ;
fragment SP             : (' ' | '\t')+ ;
PRIMITIVE               : '<primitive:' SP? DIGIT+ '>' ;
MINUS                   : '-' ;
BINARY_SELECTOR         : (
                              '\\' | '+' | '*' | '/' | '=' | '>' | '<'
                            | ',' | '@' | '%' | '~' | '|' | '&' | '?'
                          )+ ;
LT                      : '<' ;
GT                      : '>' ;
CARROT                  : '^' ;
HASH                    : '#' ;
EXP                     : 'e' ;
HEX                     : '16r' ;
LITARR_START            : '#(' ;
DYNDICT_START           : '#{' ;
DYNARR_END              : '}' ;
DYNARR_START            : '{' ;
RESERVED_WORD           
    : 'nil' | 'true' | 'false' | 'self' | 'super'
    ;
CHARACTER_CONSTANT      : '$' ~[\r\n] ;
DIGIT                   : [0-9] ;
HEXDIGIT                : [0-9a-fA-F] ;
RADIX_INT               : '-'? DIGIT+ 'r' [0-9A-Za-z]+ ;
BLOCK_PARAM             : COLON IDENTIFIER ;
COLON                   : ':' ;
ASSIGNMENT              : ':=' | '_' ;
DOLLAR                  : '$' ;
