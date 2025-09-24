grammar Smalltalk80;

// ═════════════════════════════════════════════════════════════════════════════
// Parser Semantic Predicates
// ═════════════════════════════════════════════════════════════════════════════
@parser::members {
    private bool IsMethodsFor()
    {
        // Detects the pattern: !ClassName [class] methodsFor: 'category'!
        var stream = TokenStream;
        int index = 1; // lookahead position
        if (stream.LA(index) != Smalltalk80Parser.BANG)
            return false;
        index++; // class name
        if (stream.LA(index) != Smalltalk80Parser.IDENTIFIER)
            return false;
        index++;
        // Optional 'class' keyword for metaclass methods
        if (stream.LA(index) == Smalltalk80Parser.IDENTIFIER && stream.LT(index)!.Text == "class")
            index++;
        if (stream.LA(index) != Smalltalk80Parser.IDENTIFIER)
            return false;
        var id = stream.LT(index)!.Text;
        return id == "methodsFor" && stream.LA(index + 1) == Smalltalk80Parser.COLON;
    }

    private bool NextMatchesKeyword(int offset, string keyword)
    {
        // Helper to check if the next token matches a specific keyword:value pattern
        var stream = TokenStream;
        return stream.LA(offset) == Smalltalk80Parser.IDENTIFIER
            && stream.LA(offset + 1) == Smalltalk80Parser.COLON
            && stream.LT(offset)!.Text == keyword;
    }

    private bool LooksLikeMetaclassDef()
    {
        // Detects metaclass definition: ClassName class\n\tinstanceVariableNames: ...
        var stream = TokenStream;
        return stream.LA(1) == Smalltalk80Parser.IDENTIFIER
            && stream.LA(2) == Smalltalk80Parser.IDENTIFIER
            && stream.LT(2)!.Text == "class"
            && stream.LA(3) != Smalltalk80Parser.BANG  // No bang after 'class'
            && (stream.LA(3) == Smalltalk80Parser.IDENTIFIER || stream.LA(3) == Smalltalk80Parser.Eof);
    }
}

// Helper rule for identifier:colon patterns
identifierColon: IDENTIFIER COLON;

// ═════════════════════════════════════════════════════════════════════════════
// ENTRY POINTS - Multiple entry points for different parsing contexts
// ═════════════════════════════════════════════════════════════════════════════

// Primary entry point for file-out format (complete Smalltalk-80.st file)
fileIn: chunk* EOF;

// Interactive development entry points
expressionEntry: expression EOF;      // For REPL/workspace expression evaluation
statementsEntry: statements EOF;       // For do-it blocks or workspace scripts  
methodEntry: methodHeader temporaries? primitiveDecl? methodBodyNoBang EOF; // For method editor
classEntry: classHeader classBody EOF; // For class browser/editor

// ═════════════════════════════════════════════════════════════════════════════
// FILE-OUT FORMAT CHUNKS - Top-level constructs in .st files
// ═════════════════════════════════════════════════════════════════════════════

chunk:
    metaclassDefinitionChunk    // ClassName class\n\tinstanceVariableNames: ''!
    | classDefChunk             // Full class definition with subclass:...!
    | separatorChunk            // "-- -- -- -- -- -- -- -- -- -- -- -- "!
    | simpleMethodChunk         // ClassName [class] >> methodDef (rare format)
    | expressionChunk           // Standalone expression (includes comment: expressions)!
    | methodCategoryChunk       // !ClassName methodsFor: 'category'! methods...!
    | BANG                      // Stray bang (error recovery)
    ;

// Rare method format: ClassName >> methodDefinition
simpleMethodChunk: 
    className 
    (cls = IDENTIFIER { if ($cls.text != "class") NotifyErrorListeners("expected 'class'"); })? 
    BIT_SHIFT_RIGHT 
    methodDef
    ;

// Standalone expression (e.g., initialization code)
expressionChunk: expression BANG;

// ═════════════════════════════════════════════════════════════════════════════
// CLASS DEFINITIONS - Standard Smalltalk class declaration format
// ═════════════════════════════════════════════════════════════════════════════

classDefChunk: classHeader classBody BANG;

// Class header: Object subclass: #ClassName
classHeader: className subclassKind symbolConst;

// Supported subclass types with validation
subclassKind:
    kind = identifierColon {
        var text = $kind.text;
        if (text is not ("subclass:" or "variableSubclass:" or 
                        "variableByteSubclass:" or "variableWordSubclass:"))
            NotifyErrorListeners($"invalid subclass kind: {text}");
    };

// Class body with instance/class variables and category
classBody: classSlotSpec* categoryDecl;

// Variable declarations with validation
classSlotSpec:
    spec = identifierColon stringConst {
        var text = $spec.text;
        if (text is not (
                "instanceVariableNames:" or
                "classVariableNames:" or
                "classInstanceVariableNames:" or
                "poolDictionaries:"))
            NotifyErrorListeners($"invalid slot specification: {text}");
    };

// Class category declaration
categoryDecl:
    kw = identifierColon stringConst {
        if ($kw.text != "category:")
            NotifyErrorListeners("expected 'category:'");
    };

// ═════════════════════════════════════════════════════════════════════════════
// METADATA - Metaclass definitions
// ═════════════════════════════════════════════════════════════════════════════

// Metaclass definition without leading bang
metaclassDefinitionChunk:
    { LooksLikeMetaclassDef() }? 
    className 
    cls = IDENTIFIER { if ($cls.text != "class") NotifyErrorListeners("expected 'class'"); } 
    classSlotSpec+ 
    BANG;

// ═════════════════════════════════════════════════════════════════════════════
// METHOD DEFINITIONS - Methods within category chunks
// ═════════════════════════════════════════════════════════════════════════════

// Method category chunk: !ClassName methodsFor: 'category'! methods...!
methodCategoryChunk:
    {IsMethodsFor()}? 
    BANG 
    className 
    (cls = IDENTIFIER { if ($cls.text != "class") NotifyErrorListeners("expected 'class'"); })? 
    kw = identifierColon 
    stringConst 
    BANG 
    methodDef* 
    BANG 
    {
        if ($kw.text != "methodsFor:")
            NotifyErrorListeners("expected 'methodsFor:'");
    };

// Individual method definition
methodDef:
    methodHeader 
    stringConst?  // Optional method comment
    (temporaries primitiveDecl? | primitiveDecl? temporaries?)  // Temps and/or primitive
    methodBody;

// Method body variants
methodBody:
    statements PERIOD* BANG    // Normal method with statements
    | returnStatement BANG      // Direct return: ^value!
    | BANG                      // Empty method body
    ;

// Method body for interactive development (no trailing bang)
methodBodyNoBang:
    statements PERIOD*
    | returnStatement
    ;

// Method header: unary, binary, or keyword selector
methodHeader:
    unarySelector                           // foo
    | binarySelector formalArg              // + anObject
    | (identifierColon formalArg)+          // at: index put: value
    ;

// Formal parameter name
formalArg: argumentName;

// Local variables declaration: | temp1 temp2 |
temporaries: PIPE temporaryName+ PIPE;
temporaryName: IDENTIFIER;
argumentName: IDENTIFIER;

// Primitive method declaration
primitiveDecl: PRIMITIVE_DECL;

// ═════════════════════════════════════════════════════════════════════════════
// STATEMENTS - Method body content
// ═════════════════════════════════════════════════════════════════════════════

// Statement sequence with optional periods
statements: PERIOD* statement (PERIOD+ statement)* PERIOD*;

// Statement types
statement: 
    returnStatement        // ^expression
    | expressionStatement  // any expression
    ;

// Return statement: ^expression
returnStatement: CARET expression PERIOD?;

// Expression as statement
expressionStatement: expression;

// ═════════════════════════════════════════════════════════════════════════════
// EXPRESSIONS - Core Smalltalk expression hierarchy
// ═════════════════════════════════════════════════════════════════════════════

// Top-level expression with assignment
expression: assignmentExpr;

// Assignment expression (right-associative)
assignmentExpr:
    variable ASSIGN assignmentExpr  // var _ expr or var := expr
    | cascadeExpr                    // non-assignment expression
    ;

// Cascade expression: receiver msg1; msg2; msg3
cascadeExpr: messageExpr (SEMICOLON messagePart)*;

// ═════════════════════════════════════════════════════════════════════════════
// MESSAGE EXPRESSIONS - Smalltalk message passing
// ═════════════════════════════════════════════════════════════════════════════

// Message expression with precedence: unary > binary > keyword
messageExpr:
    messageBase (unaryMessage | binaryMessage)* keywordMessage?;

// Base expression that can receive messages
messageBase:
    literal             // Numbers, strings, symbols, arrays
    | parenthesizedExpr // (expression)
    | block             // [:arg | statements]
    | dynamicArray      // {expr. expr. expr}
    | variable          // identifier
    | negativeNumber    // -123 or -variable
    ;

// Negative number or negated expression
negativeNumber: MINUS messageBase;

// Message parts for cascades
messagePart: 
    unaryMessage 
    | binaryMessage 
    | keywordMessage
    ;

// ═════════════════════════════════════════════════════════════════════════════
// MESSAGE TYPES - Three kinds of Smalltalk messages
// ═════════════════════════════════════════════════════════════════════════════

// Unary message: receiver selector
unaryMessage: unarySelector;

// Binary message: receiver operator argument
binaryMessage: binarySelector messageBase;

// Keyword message: receiver key1: arg1 key2: arg2
keywordMessage: keywordPart+;

// Single keyword:argument pair
keywordPart: IDENTIFIER COLON nonKeywordExpr;

// Expression for keyword arguments (no nested keywords for precedence)
nonKeywordExpr: messageBase (unaryMessage | binaryMessage)*;

// ═════════════════════════════════════════════════════════════════════════════
// BLOCKS AND PRIMARY EXPRESSIONS - Closures and grouping
// ═════════════════════════════════════════════════════════════════════════════

// Block closure: [:arg1 :arg2 | | temp | statements]
block:
    LEFT_BRACKET 
    blockArgs?     // Block arguments
    blockTemps?    // Block temporaries
    statements?    // Block body
    RIGHT_BRACKET;

// Block arguments: :arg1 :arg2 |
blockArgs: (COLON argumentName)+ PIPE?;

// Block temporaries: | temp1 temp2 |
blockTemps: PIPE temporaryName+ PIPE;

// Parenthesized expression
parenthesizedExpr: LEFT_PAREN expression RIGHT_PAREN;

// Dynamic array: {expr1. expr2. expr3}
dynamicArray: LEFT_BRACE (expression (PERIOD expression)*)? RIGHT_BRACE;

// ═════════════════════════════════════════════════════════════════════════════
// SELECTORS - Message names
// ═════════════════════════════════════════════════════════════════════════════

// Unary selector: simple identifier
unarySelector: IDENTIFIER;

// Binary selector: operator symbols
binarySelector:
    // Arithmetic operators
    PLUS           // +
    | MINUS        // -
    | STAR         // *
    | DIVIDE       // /
    | MOD          // %
    | INTEGER_DIVIDE  // //
    | MODULO       // \\
    
    // Bitwise operators
    | BIT_SHIFT_LEFT   // <<
    | BIT_SHIFT_RIGHT  // >>
    | BACKSLASH        // \
    | TILDE            // ~
    | PIPE             // |
    | BIT_AND          // &
    
    // Comparison operators
    | EQUAL            // =
    | NOT_EQUAL        // ~=
    | EQUAL_EQUAL      // ==
    | NOT_IDENTITY     // ~~
    | LT               // <
    | LEQ              // <=
    | GT               // >
    | GEQ              // >=
    
    // Other operators
    | AT               // @
    | QUESTION         // ?
    | COMMA            // ,
    ;

// Variable reference
variable: IDENTIFIER;

// ═════════════════════════════════════════════════════════════════════════════
// LITERALS - Constant values
// ═════════════════════════════════════════════════════════════════════════════

literal:
    numberLiteral       // 123, 3.14, 16rFF
    | stringConst       // 'hello world'
    | symbolConst       // #symbol or #'complex symbol'
    | arrayLiteral      // #(1 2 3)
    | byteArrayLiteral  // #[1 2 3]
    | characterLiteral  // $a, $!, $\
    ;

// Numbers: integers, floats, radix notation
numberLiteral: NUMBER;

// String constant
stringConst: STRING;

// Character literal: $a, $!, $\, etc.
characterLiteral: CHAR_LITERAL;

// Byte array: #[1 2 255]
byteArrayLiteral: HASH LEFT_BRACKET NUMBER* RIGHT_BRACKET;

// Literal array: #(1 'string' #symbol (nested array))
arrayLiteral: HASH LEFT_PAREN arrayElement* RIGHT_PAREN;

// Array element: literals, symbols, or nested arrays
arrayElement:
    literal
    | symbolName
    | LEFT_PAREN arrayElement* RIGHT_PAREN
    ;

// Symbol constant: #symbol or #'complex symbol'
symbolConst: QUOTED_SYMBOL | HASH symbolName;

// Symbol name variants
symbolName:
    IDENTIFIER
    | unarySelector
    | binarySelector
    | keywordSymbolSelector
    ;

// Keyword symbol: #at:put:
keywordSymbolSelector: (IDENTIFIER COLON)+;

// ═════════════════════════════════════════════════════════════════════════════
// SPECIAL CHUNKS - Separators and utilities
// ═════════════════════════════════════════════════════════════════════════════

// Separator line in file-out format
separatorChunk: SEPARATOR_LINE BANG;

// Class name reference
className: IDENTIFIER;

// ═════════════════════════════════════════════════════════════════════════════
// LEXER RULES - Token definitions
// ═════════════════════════════════════════════════════════════════════════════

// Assignment operators
ASSIGN: '_' | ':=';

// Multi-character operators (must come before single-char versions)
BIT_SHIFT_LEFT: '<<';
BIT_SHIFT_RIGHT: '>>';
INTEGER_DIVIDE: '\\\\';  // Escaped for ANTLR
MODULO: '//';
EQUAL_EQUAL: '==';
NOT_IDENTITY: '~~';
LEQ: '<=';
GEQ: '>=';
NOT_EQUAL: '~=';

// Single-character operators
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

// Special characters
CARET: '^';
HASH: '#';
BANG: '!';
COLON: ':';
SEMICOLON: ';';
PERIOD: '.';

// Brackets and parentheses
LEFT_BRACKET: '[';
RIGHT_BRACKET: ']';
LEFT_PAREN: '(';
RIGHT_PAREN: ')';
LEFT_BRACE: '{';
RIGHT_BRACE: '}';

// Primitive declaration: <primitive: 123>
PRIMITIVE_DECL:
    '<' WS_INLINE* 'primitive:' WS_INLINE* DIGITS WS_INLINE* '>';

// Separator line pattern
SEPARATOR_LINE:
    '"' '-'+ (' ' '-'+)* ' '* '"';

// Numbers with radix, decimal, and scientific notation
NUMBER:
    DIGITS [rR] BASEDIGIT+                                          // Radix: 16rFF
    | DIGITS ('.' DIGITS)? (('e' | 'E') ('+' | '-')? DIGITS)?     // Decimal/float
    | DIGITS ('s' DIGITS)?                                          // Scaled decimal
    ;

// Comments (hidden channel for preservation)
COMMENT: '"' ('""' | ~'"')* '"' -> channel(HIDDEN);

// String literals with doubled quotes for escaping
STRING: '\'' ( '\'\'' | ~'\'')* '\'';

// Quoted symbol: #'complex symbol'
QUOTED_SYMBOL: HASH '\'' ( '\'\'' | ~'\'')* '\'';

// Character literals with special cases
CHAR_LITERAL:
    '$!!'          // Special case: doubled bang -> single bang character
    | '$' ~[\r\n]   // Any character after $
    ;

// Identifiers
IDENTIFIER: LETTER (LETTER | DIGIT)*;

// Fragments for lexer rules
fragment LETTER: [A-Za-z];
fragment DIGIT: [0-9];
fragment BASEDIGIT: [0-9A-Za-z];
fragment WS_INLINE: [ \t\f];
DIGITS: DIGIT+;

// Whitespace handling
NEW_LINE: ('\r'? '\n')+ -> skip;
WS: [ \t\f]+ -> skip;