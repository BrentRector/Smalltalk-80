grammar Smalltalk;

//----------------- Parser rules -----------------

method
    : messagePattern ( temporaries statements )? EOF
    ;

// A method header can be:
//   • unarySelector
//   • binarySelector variableName
//   • one-or-more IDENTIFIER:variableName segments
messagePattern
    : unarySelector
    | binarySelector variableName
    | ( IDENTIFIER ':' variableName )+
    ;

expression
    : variableName ':=' expression               // assignment
    | primary ( messageExpression | cascadedMessageExpression )?
    ;

messageExpression
    : unaryObjectDescription
      ( unarySelector
      | binarySelector unaryObjectDescription
      | keyword unaryObjectDescription
      )*
    ;

cascadedMessageExpression
    : messageExpression
      ( '.' (
          unarySelector
        | binarySelector unaryObjectDescription
        | keyword binaryObjectDescription
        )
      )+
    ;

primary
    : variableName
    | literal
    | block
    | '(' expression ')'
    ;

unaryObjectDescription
    : primary
    | unaryObjectDescription unarySelector
    ;

binaryObjectDescription
    : unaryObjectDescription
    | binaryObjectDescription binarySelector unaryObjectDescription
    ;

keywordExpression
    : binaryObjectDescription ( keyword binaryObjectDescription )*
    ;

statements
    : statement ( '.' statement )*
    ;
statement
    : '!'? expression
    ;

block
    : '[' ( ':' variableName )* ( '|' variableName+ '|' )? statements ']'
    ;

temporaries
    : '|' variableName+ '|'
    ;

literal
    : number
    | symbolConstant
    | characterConstant
    | STRING
    | arrayConstant
    ;

array
    : '(' ( number
          | symbol
          | STRING
          | characterConstant
          | array
          )* ')'
    ;
arrayConstant
    : '#' array
    ;

//----------------- Lexer rules -----------------

// Fundamental fragments
fragment DIGIT   : [0-9] ;
fragment LETTER  : [A-Za-z] ;

// Exactly these 12 special characters (no minus)
fragment SPECIAL : [+\/*~<>=@%|&?!] ;

// “character” = any digit, letter, special, or these exact symbols:
//   [ ] { } ( )  –  ;  $  !  #  :
fragment CHARACTER
    : DIGIT
    | LETTER
    | SPECIAL
    | [\[\]\{\}\(\)\-\;\$\!\#\:]
    ;

// Identifiers & variable names
IDENTIFIER
    : LETTER ( LETTER | DIGIT )*
    ;
variableName
    : IDENTIFIER
    ;

// Selectors & keywords
unarySelector
    : IDENTIFIER
    ;

binarySelector
    : '-'                // a single minus
    | SPECIAL            // exactly one special character
    | SPECIAL SPECIAL    // exactly two special characters
    ;

keyword
    : ( IDENTIFIER ':' )+  // one or more identifier: segments
    ;

// Symbols (for array elements, etc.)
symbol
    : IDENTIFIER
    | binarySelector
    | keyword
    ;
symbolConstant
    : '#' symbol
    ;

// Character constants begin with $
characterConstant
    : '$' ( CHARACTER | '\'' | '"' )
    ;

// Number literals: digits [r[-]digits] [ .digits ] [ e[-]digits ]
number
    : DIGIT+ ( 'r' '-'? DIGIT+ )? ( '.' DIGIT+ )? ( 'e' '-'? DIGIT+ )?
    ;

// String literals use '…' with '' for embedded quote
STRING
    : '\'' ( CHARACTER | '\'\'' )* '\''
    ;

// Comments use "…" with "" for embedded double-quote and ' for an embedded single-quote
COMMENT
    : '"' ( CHARACTER    // any digit/letter/special
           | '""'        // embedded double-quote
           | '\''        // embedded single-quote
           )* '"'
    -> skip
    ;

// Whitespace
WS
    : [ \t\r\n]+ -> skip
    ;
