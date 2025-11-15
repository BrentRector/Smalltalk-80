// =============================================================================
// Smalltalk-80 Lexer Grammar
// =============================================================================
// Purpose: Tokenize Smalltalk-80 source with context-sensitive symbol handling
// Author: Generated for Smalltalk-80 Interpreter/Compiler
// Version: 3.0 - Split from combined grammar to support lexer modes
// =============================================================================

lexer grammar Smalltalk80Lexer;

// =============================================================================
// DEFAULT MODE - Standard Smalltalk tokens
// =============================================================================

// -----------------------------------------------------------------------------
// Multi-character operators (must come before single-character)
// -----------------------------------------------------------------------------
ASSIGN: '_' | ':=';
BIT_SHIFT_LEFT: '<<';
BIT_SHIFT_RIGHT: '>>';
INTEGER_DIVIDE: '\\\\';
MODULO: '//';
EQUAL_EQUAL: '==';
NOT_IDENTITY: '~~';
LEQ: '<=';
GEQ: '>=';
NOT_EQUAL: '~=';

// -----------------------------------------------------------------------------
// Single-character operators
// -----------------------------------------------------------------------------
PLUS: '+';
MINUS: '-';
STAR: '*';
DIVIDE: '/';
MOD: '%';
BACKSLASH: '\\';
TILDE: '~';
PIPE: '|';
BIT_AND: '&';
EQUAL: '=';
LT: '<';
GT: '>';
AT: '@';
QUESTION: '?';
COMMA: ',';
CARET: '^';
HASH: '#' -> pushMode(SYMBOL_MODE);  // Enter symbol mode after #
BANG: '!';
COLON: ':';

// -----------------------------------------------------------------------------
// Delimiters
// -----------------------------------------------------------------------------
LEFT_BRACKET: '[';
RIGHT_BRACKET: ']';
LEFT_PAREN: '(';
RIGHT_PAREN: ')';
LEFT_BRACE: '{';
RIGHT_BRACE: '}';
SEMICOLON: ';';
PERIOD: '.';

// -----------------------------------------------------------------------------
// Primitive declaration
// -----------------------------------------------------------------------------
PRIMITIVE_DECL
    : '<' WS_FRAG* 'primitive:' WS_FRAG* DIGITS WS_FRAG* '>'
    ;

// -----------------------------------------------------------------------------
// Numbers
// -----------------------------------------------------------------------------
NUMBER
    : DIGITS [rR] BASEDIGIT+                                              // Radix notation: 16rFF
    | DIGITS ('.' DIGITS)? (('e' | 'E') ('+' | '-')? DIGITS)?           // Decimal/float
    | DIGITS 's' DIGITS                                                   // Scaled decimal
    ;

// -----------------------------------------------------------------------------
// Comments (preserved on hidden channel)
// -----------------------------------------------------------------------------
COMMENT
    : '"' ('""' | ~'"')* '"' -> channel(HIDDEN)
    ;

// -----------------------------------------------------------------------------
// Strings
// -----------------------------------------------------------------------------
STRING
    : '\'' ( '\'\'' | ~'\'')* '\''  // Single quotes with doubling for escape
    ;

// -----------------------------------------------------------------------------
// Character literals
// -----------------------------------------------------------------------------
CHAR_LITERAL
    : '$!!'          // Special case: $! followed by ! needs escaping
    | '$' ~[\r\n]     // Normal character literal
    ;

// -----------------------------------------------------------------------------
// Separator line pattern for file-out format
// -----------------------------------------------------------------------------
SEPARATOR_LINE
    : '"' '-'+ (' ' '-'+)* ' '* '"'
    ;

// -----------------------------------------------------------------------------
// Identifiers
// -----------------------------------------------------------------------------
IDENTIFIER
    : LETTER (LETTER | DIGIT)*
    ;

// -----------------------------------------------------------------------------
// Whitespace
// -----------------------------------------------------------------------------
NEW_LINE
    : ('\r'? '\n')+ -> skip
    ;

WS
    : [ \t\f]+ -> skip
    ;

// =============================================================================
// SYMBOL MODE - Context-sensitive tokenization after #
// =============================================================================
// This mode solves the ambiguity between:
//   - Multi-keyword symbols: #at:put: (all keywords consumed as one symbol)
//   - Single keyword in message: retry: #quo: coercing: aNumber
//
// The mode tokenizes symbol content greedily until whitespace/delimiter,
// then pops back to default mode. This allows:
//   #quo:coercing:  → SYMBOL_KEYWORD_SELECTOR("quo:coercing:")
//   #quo: coercing: → SYMBOL_KEYWORD_SELECTOR("quo:"), then exits mode
// =============================================================================

mode SYMBOL_MODE;

// Keyword selector symbol - greedily captures keyword pairs until whitespace
// Examples: #at:put:, #quo:, #retry:coercing:with:
// The lexer consumes all consecutive identifier:identifier: pairs
SYMBOL_KEYWORD_SELECTOR
    : LETTER (LETTER | DIGIT)* ':' (LETTER (LETTER | DIGIT)* ':')* -> popMode
    ;

// Regular identifier symbol
// Examples: #foo, #Point, #x
SYMBOL_IDENTIFIER
    : LETTER (LETTER | DIGIT)* -> type(IDENTIFIER), popMode
    ;

// =============================================================================
// Binary Operator Symbol Rules (SYMBOL_MODE)
// =============================================================================
// Examples: #+, #-, #<=, #~~, #*, #/
//
// DESIGN RATIONALE: Why 25 separate rules instead of consolidation?
//
// The type() command in ANTLR4 lexer rules performs TOKEN TYPE REMAPPING:
//   SYMBOL_PLUS: '+' -> type(PLUS), popMode;
// This means: "When you match '+' in SYMBOL_MODE, emit a PLUS token (not SYMBOL_PLUS)"
//
// This allows the parser to use the SAME token vocabulary for both:
//   - Symbol literals: #+ → PLUS token in symbol context
//   - Actual operators: 3 + 4 → PLUS token in expression context
//
// ANTLR4 LIMITATION: The type() command requires LITERAL token type names.
// We cannot use:
//   - Fragments that reference token names
//   - Programmatic type mapping (would require embedded code)
//   - Dynamic token type selection based on matched text
//
// ALTERNATIVE CONSIDERED: Single rule emitting SYMBOL_BINARY tokens
//   SYMBOL_BINARY: ('<<'|'>>'|'+' ...) -> popMode;
// This would require:
//   - Parser changes to accept SYMBOL_BINARY instead of specific tokens
//   - Loss of parser token vocabulary consistency
//   - Additional semantic analysis to distinguish #+, #-, etc.
//
// CONCLUSION: The 25 explicit rules are the cleanest ANTLR4 solution.
// The apparent redundancy ensures correct token type mapping without
// requiring embedded code or parser modifications.
//
// Multi-character operators MUST come before single-character (lexer precedence)
SYMBOL_BIT_SHIFT_LEFT: '<<' -> type(BIT_SHIFT_LEFT), popMode;
SYMBOL_BIT_SHIFT_RIGHT: '>>' -> type(BIT_SHIFT_RIGHT), popMode;
SYMBOL_INTEGER_DIVIDE: '\\\\' -> type(INTEGER_DIVIDE), popMode;
SYMBOL_MODULO: '//' -> type(MODULO), popMode;
SYMBOL_EQUAL_EQUAL: '==' -> type(EQUAL_EQUAL), popMode;
SYMBOL_NOT_IDENTITY: '~~' -> type(NOT_IDENTITY), popMode;
SYMBOL_LEQ: '<=' -> type(LEQ), popMode;
SYMBOL_GEQ: '>=' -> type(GEQ), popMode;
SYMBOL_NOT_EQUAL: '~=' -> type(NOT_EQUAL), popMode;
// Single-character operators
SYMBOL_PLUS: '+' -> type(PLUS), popMode;
SYMBOL_MINUS: '-' -> type(MINUS), popMode;
SYMBOL_STAR: '*' -> type(STAR), popMode;
SYMBOL_DIVIDE: '/' -> type(DIVIDE), popMode;
SYMBOL_MOD: '%' -> type(MOD), popMode;
SYMBOL_BACKSLASH: '\\' -> type(BACKSLASH), popMode;
SYMBOL_TILDE: '~' -> type(TILDE), popMode;
SYMBOL_PIPE: '|' -> type(PIPE), popMode;
SYMBOL_BIT_AND: '&' -> type(BIT_AND), popMode;
SYMBOL_EQUAL: '=' -> type(EQUAL), popMode;
SYMBOL_LT: '<' -> type(LT), popMode;
SYMBOL_GT: '>' -> type(GT), popMode;
SYMBOL_AT: '@' -> type(AT), popMode;
SYMBOL_QUESTION: '?' -> type(QUESTION), popMode;
SYMBOL_COMMA: ',' -> type(COMMA), popMode;

// Left paren starts array literal: #(1 2 3)
SYMBOL_LPAREN
    : '(' -> type(LEFT_PAREN), popMode
    ;

// Left bracket starts byte array literal: #[1 2 255]
SYMBOL_LBRACK
    : '[' -> type(LEFT_BRACKET), popMode
    ;

// Quoted symbol: #'complex symbol with spaces'
SYMBOL_QUOTE
    : '\'' ( '\'\'' | ~'\'')* '\'' -> type(QUOTED_SYMBOL), popMode
    ;

// *** KEY TO SOLVING AMBIGUITY ***
// Whitespace EXITS symbol mode - this is how we distinguish:
//   #quo:coercing: - no space, one symbol
//   #quo: coercing: - space after quo:, exits mode, coercing: is separate
SYMBOL_WS
    : [ \t\f\r\n]+ -> skip, popMode
    ;

// Any other character ends symbol mode
SYMBOL_OTHER
    : . -> skip, popMode
    ;

// =============================================================================
// Lexer Fragments (shared utilities)
// =============================================================================

fragment LETTER: [A-Za-z];
fragment DIGIT: [0-9];
fragment BASEDIGIT: [0-9A-Za-z];
fragment WS_FRAG: [ \t\f];
fragment BINARY_OP: [+\-*/<>=~@%|&?,\\!];

DIGITS: DIGIT+;

// Quoted symbol token (used when SYMBOL_QUOTE emits this type)
QUOTED_SYMBOL
    : {false}? . // Never matched directly, only via type() in SYMBOL_QUOTE
    ;
