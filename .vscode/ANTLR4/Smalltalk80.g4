// Smalltalk80.g4 — Full Smalltalk-80 Grammar for ANTLR4
grammar Smalltalk80;

IDENTIFIER      : [a-zA-Z_] [a-zA-Z0-9_]* ;
NUMBER          : '-'? DIGIT+ ('.' DIGIT+)? ;
STRING          : '\'' ( ~[\'] | '\'\'' )* '\'' ;
SYMBOL          : '#' ( IDENTIFIER | BINARY_SELECTOR | STRING ) ;
CHARACTER_CONST : '$' . ;
WS              : [ \t\r\n]+ -> skip ;

fragment DIGIT  : [0-9] ;

BINARY_SELECTOR : ( '+' | '-' | '*' | '/' | '~' | '<' | '>' | '=' | '@' | '%' | '|' | '&' | '?' | '!' )+ ;
KEYWORD         : IDENTIFIER ':' ;

LPAREN          : '(' ;
RPAREN          : ')' ;
LBRACK          : '[' ;
RBRACK          : ']' ;
PIPE            : '|' ;
COLON           : ':' ;
ASSIGN          : ':=' ;
DOT             : '.' ;
SEMICOLON       : ';' ;
CARAT           : '^' ;

program         : (classDefinition | methodDefinition | expressionList)* EOF ;

expressionList  : expression (DOT expression)* ;

expression
    : assignment
    | returnExpr
    | cascade ;

assignment      : IDENTIFIER ASSIGN expression ;

returnExpr      : CARAT expression ;

cascade         : message (SEMICOLON message)* ;

message         : primary (unaryMessage | binaryMessage | keywordMessage)* ;

unaryMessage    : IDENTIFIER ;
binaryMessage   : BINARY_SELECTOR primary ;
keywordMessage  : (KEYWORD primary)+ ;

primary
    : IDENTIFIER
    | literal
    | block
    | LPAREN expression RPAREN
    | 'self'
    | 'super'
    | 'nil'
    | 'true'
    | 'false' ;

literal
    : NUMBER
    | STRING
    | SYMBOL
    | CHARACTER_CONST
    | arrayLiteral ;

arrayLiteral    : '#' LPAREN literal* RPAREN ;

block
    : LBRACK (blockArgs PIPE)? (temporaries)? blockBody RBRACK ;

blockArgs       : (COLON IDENTIFIER)+ ;

blockBody       : (expressionList DOT)* (returnExpr | expression)? ;

methodDefinition
    : methodHeader methodBody ;

methodHeader
    : unarySelector
    | binarySelector IDENTIFIER
    | (KEYWORD IDENTIFIER)+ ;

methodBody
    : (primitiveDeclaration)? (temporaries)? statements ;

primitiveDeclaration
    : '<primitive:' NUMBER '>' ;

temporaries
    : PIPE IDENTIFIER+ PIPE ;

statements
    : expressionList ;

classDefinition
    : 'Class' IDENTIFIER
      'subclass:' STRING
      'instanceVariableNames:' STRING
      'classVariableNames:' STRING
      'poolDictionaries:' STRING
      'category:' STRING
      (classMethodDictionary)? ;

classMethodDictionary
    : 'methodsFor:' STRING methodList ;

methodList
    : (methodDefinition DOT)* ;

unarySelector   : IDENTIFIER ;
binarySelector  : BINARY_SELECTOR ;
