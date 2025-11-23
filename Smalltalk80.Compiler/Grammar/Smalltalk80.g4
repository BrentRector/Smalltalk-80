// =============================================================================
// Smalltalk-80 Comprehensive Grammar
// =============================================================================
// Purpose: Parse both file-out format and interactive Smalltalk-80 code
// Author: Generated for Smalltalk-80 Interpreter/Compiler
// Version: 2.0 - Complete rewrite with multiple entry points
//
// This grammar successfully parses the entire 42,161-line Smalltalk-80.st
// file-out source while also supporting interactive development scenarios.
// =============================================================================

grammar Smalltalk80;

// =============================================================================
// Parser Semantic Predicates
// =============================================================================
// These C# predicates help disambiguate complex patterns in file-out format

@parser::members {
    private bool IsMethodsFor()
    {
        var stream = TokenStream;
        int index = 1; // lookahead position
        if (stream.LA(index) != Smalltalk80Parser.BANG)
            return false;
        index++; // class name
        if (stream.LA(index) != Smalltalk80Parser.IDENTIFIER)
            return false;
        index++;
        if (stream.LA(index) == Smalltalk80Parser.IDENTIFIER && stream.LT(index)!.Text == "class")
            index++;
        if (stream.LA(index) != Smalltalk80Parser.IDENTIFIER)
            return false;
        var id = stream.LT(index)!.Text;
        return id == "methodsFor" && stream.LA(index + 1) == Smalltalk80Parser.COLON;
    }

    private bool NextMatchesKeyword(int offset, string keyword)
    {
        var stream = TokenStream;
        return stream.LA(offset) == Smalltalk80Parser.IDENTIFIER
            && stream.LA(offset + 1) == Smalltalk80Parser.COLON
            && stream.LT(offset)!.Text == keyword;
    }

    private bool LooksLikeMetaclassDef()
    {
        var stream = TokenStream;
        return stream.LA(1) == Smalltalk80Parser.IDENTIFIER
            && stream.LA(2) == Smalltalk80Parser.IDENTIFIER
            && stream.LA(3) == Smalltalk80Parser.IDENTIFIER
            && stream.LA(4) == Smalltalk80Parser.COLON;
    }
}

// =============================================================================
// ENTRY POINTS - Multiple contexts for different tools and use cases
// =============================================================================

// -----------------------------------------------------------------------------
// Primary File-Out Entry Point
// -----------------------------------------------------------------------------
// Purpose: Parse complete .st file-out format files
// Context: Used by compiler to process system sources
// Example: The entire Smalltalk-80.st file
fileIn
    : chunk* EOF
    ;

// -----------------------------------------------------------------------------
// Interactive Development Entry Points
// -----------------------------------------------------------------------------

// Single expression evaluation (Inspector, Workspace PrintIt)
// Example: 3 + 4
// Example: self class methods size
expressionEntry
    : expression EOF
    ;

// Multiple statements with temporaries (Workspace DoIt)
// Example: | temp | temp := 100. Transcript show: temp sqrt
statementsEntry
    : statements EOF
    ;

// Method editing in browser (no file-out bangs)
// Example: factorial
//          self = 0 ifTrue: [^1].
//          ^self * (self - 1) factorial
methodEntry
    : methodHeader temporaries? primitiveDecl? methodBodyNoBang EOF
    ;

// Class definition in browser
// Example: Object subclass: #Point instanceVariableNames: 'x y'
classEntry
    : classHeader classBody EOF
    ;

// -----------------------------------------------------------------------------
// Specialized Tool Entry Points
// -----------------------------------------------------------------------------

// Block editor in debugger/inspector
// Example: [:x :y | x + y]
blockEntry
    : block EOF
    ;

// Selector browser/method finder
// Example: at:put:
selectorEntry
    : selector EOF
    ;

// Unified selector rule for all types
selector
    : unarySelector
    | binarySelector
    | keywordSelector
    ;

// Keyword selector for finder
keywordSelector
    : (IDENTIFIER COLON)+
    ;

// Workspace with explicit temporaries
// Example: | a b | a := 5. b := 10. a + b
temporariesEntry
    : temporaries? statements EOF
    ;

// Cascade builder/refactoring tool
// Example: Transcript cr; show: 'Hello'; cr
cascadeEntry
    : cascadeExpr EOF
    ;

// Literal inspector
// Example: #(1 2 3)
// Example: 'Hello World'
literalEntry
    : literal EOF
    ;

// =============================================================================
// FILE-OUT FORMAT CHUNKS
// =============================================================================
// These rules handle the '!' delimited chunk format of file-out files

// A chunk is any top-level construct in file-out format
chunk
    : metaclassCommentChunk     // Foo class comment: '...'!
    | metaclassDefinitionChunk  // Foo class instanceVariableNames: ''!
    | classCommentChunk         // !Foo comment: '...'!
    | classDefChunk            // Object subclass: #Foo ...!
    | simpleMethodChunk        // For direct method definitions
    | expressionChunk          // Initialization expressions
    | methodCategoryChunk      // !Foo methodsFor: 'accessing'! ... !
    | separatorChunk           // "-- -- -- -- -- -- -- -- -- -- -- -- "!
    | BANG                     // Empty chunks (just !)
    ;

// Direct method definition (rare in file-out)
// Example: ClassName>>method
simpleMethodChunk
    : className (cls = IDENTIFIER { if ($cls.text != "class") NotifyErrorListeners("expected 'class'"); })? 
      BIT_SHIFT_RIGHT methodDef
    ;

// Initialization/DoIt expression
// Example: Smalltalk at: #Foo put: nil!
expressionChunk
    : expression BANG
    ;

// Class definition chunk
// Example: Object subclass: #Point instanceVariableNames: 'x y' ...!
classDefChunk
    : classHeader classBody BANG
    ;

// Class header specifies superclass relationship
// Smalltalk-80 syntax: SuperclassName subclass: #NewClassName
classHeader
    : superclassName subclassKind symbolConst
    ;

// Types of subclass relationships
subclassKind
    : kind = identifierColon {
          var text = $kind.text;
          if (text is not ("subclass:" or "variableSubclass:" or "variableByteSubclass:" or "variableWordSubclass:"))
              NotifyErrorListeners($"invalid subclass kind: {text}");
      }
    ;

// Class body contains variable declarations and category
classBody
    : classSlotSpec* categoryDecl
    ;

// Variable specifications
classSlotSpec
    : spec = identifierColon stringConst {
          var text = $spec.text;
          if (text is not (
                  "instanceVariableNames:" or
                  "classVariableNames:" or
                  "classInstanceVariableNames:" or
                  "poolDictionaries:"))
              NotifyErrorListeners($"invalid slot specification: {text}");
      }
    ;

// Category declaration
categoryDecl
    : kw = identifierColon stringConst {
          if ($kw.text != "category:")
              NotifyErrorListeners("expected 'category:'");
      }
    ;

// =============================================================================
// COMMENTS AND METADATA
// =============================================================================

// Class comment with leading bang
// Example: !Point comment: 'I represent a 2D coordinate'!
classCommentChunk
    : { NextMatchesKeyword(2, "comment") }? 
      BANG className kw = identifierColon NEW_LINE? stringConst BANG
      {
          if ($kw.text != "comment:")
              NotifyErrorListeners("expected 'comment:'");
      }
    ;

// Metaclass definition (no leading bang)
// Example: Point class instanceVariableNames: 'origin'!
metaclassDefinitionChunk
    : { LooksLikeMetaclassDef() }? 
      className cls = IDENTIFIER { if ($cls.text != "class") NotifyErrorListeners("expected 'class'"); } 
      classSlotSpec+ BANG
    ;

// Metaclass comment (no leading bang)
// Example: Point class comment: 'Class-side documentation'!
metaclassCommentChunk
    : { NextMatchesKeyword(3, "comment") }? 
      className cls = IDENTIFIER { if ($cls.text != "class") NotifyErrorListeners("expected 'class'"); } 
      kw = identifierColon NEW_LINE? stringConst BANG 
      {
          if ($kw.text != "comment:")
              NotifyErrorListeners("expected 'comment:'");
      }
    ;

// =============================================================================
// METHOD DEFINITIONS
// =============================================================================

// Method definition in file-out format (with bang)
methodDef
    : methodHeader stringConst? NEW_LINE? 
      (temporaries primitiveDecl? | primitiveDecl? temporaries?) 
      methodBody
    ;

// Method body variants
methodBody
    : statements PERIOD* BANG    // Standard method
    | returnStatement BANG        // Short return form
    | BANG                       // Empty method
    ;

// Method body for browser entry (no bang)
methodBodyNoBang
    : statements PERIOD*         // Standard statements
    | returnStatement           // Return statement
    ;

// Method category chunk - contains multiple methods
// Example: !Point methodsFor: 'accessing'! x ^x! y ^y! !
methodCategoryChunk
    : {IsMethodsFor()}? BANG className 
      (cls = IDENTIFIER { if ($cls.text != "class") NotifyErrorListeners("expected 'class'"); })?
      kw = identifierColon stringConst BANG 
      (methodDef)* BANG 
      {
          if ($kw.text != "methodsFor:")
              NotifyErrorListeners("expected 'methodsFor:'");
      }
    ;

// Method headers - three types in Smalltalk
methodHeader
    : unarySelector                    // factorial
    | binarySelector formalArg          // + aNumber
    | (identifierColon formalArg)+     // at: index put: value
    ;

// Formal argument name
formalArg
    : argumentName
    ;

// Primitive declaration
// Example: <primitive: 60>
primitiveDecl
    : PRIMITIVE_DECL
    ;

// Temporaries declaration
// Example: | temp1 temp2 |
temporaries
    : PIPE temporaryName+ PIPE NEW_LINE?
    ;

temporaryName
    : IDENTIFIER
    ;

// =============================================================================
// STATEMENTS AND EXPRESSIONS
// =============================================================================

// Statement sequences
statements
    : PERIOD* statement (PERIOD+ statement)* PERIOD*
    ;

// Individual statements
statement
    : returnStatement      // ^expr
    | expressionStatement  // expr
    ;

// Return statement
returnStatement
    : CARET expression PERIOD?
    ;

// Expression as statement
expressionStatement
    : expression
    ;

// =============================================================================
// EXPRESSION HIERARCHY
// =============================================================================
// Precedence: assignment < cascade < messages

// Top-level expression with assignment
expression
    : assignmentExpr
    ;

// Assignment expression (right-associative)
// Example: x := y := 5
assignmentExpr
    : variable ASSIGN assignmentExpr
    | cascadeExpr
    ;

// Cascade expression
// Example: stream nextPut: $a; nextPut: $b; cr
cascadeExpr
    : messageExpr (SEMICOLON messagePart)*
    ;

// =============================================================================
// MESSAGE EXPRESSIONS
// =============================================================================
// Smalltalk precedence: unary > binary > keyword
// Grammar structure enforces precedence: unaryExpr contains binaryExpr contains keywordExpr

// Complete message expression (may include keyword message)
messageExpr
    : binaryExpr keywordMessage?
    ;

// Binary expression: unary expressions connected by binary messages
binaryExpr
    : unaryExpr (binaryMessage)*
    ;

// Unary expression: message base with unary messages
unaryExpr
    : messageBase unaryMessage*
    ;

// Message base (receiver)
messageBase
    : literal
    | parenthesizedExpr
    | block
    | dynamicArray
    | variable
    | negativeNumber  // Special case for negative literals
    ;

// Negative number (special handling)
negativeNumber
    : MINUS messageBase
    ;

// Message parts for cascades
messagePart
    : unaryMessage
    | binaryMessage
    | keywordMessage
    ;

// =============================================================================
// MESSAGE TYPES
// =============================================================================

// Unary message
// Example: size, isEmpty
unaryMessage
    : unarySelector
    ;

// Binary message
// Example: + 5, = true
// Argument is a unaryExpr to enforce precedence
binaryMessage
    : binarySelector unaryExpr
    ;

// Keyword message
// Example: at: 5 put: 'hello'
keywordMessage
    : keywordPart+
    ;

// Keyword part with argument
keywordPart
    : IDENTIFIER COLON binaryExpr
    ;

// Non-keyword expression (prevents keyword nesting) - now uses binaryExpr
nonKeywordExpr
    : binaryExpr
    ;

// =============================================================================
// BLOCKS
// =============================================================================

// Block closure
// Example: [x + y]
// Example: [:a :b | a max: b]
block
    : LEFT_BRACKET blockArgs? blockTemps? statements? RIGHT_BRACKET
    ;

// Block arguments
blockArgs
    : (COLON argumentName)+ PIPE?
    ;

// Block temporaries
blockTemps
    : PIPE temporaryName+ PIPE
    ;

// =============================================================================
// PRIMARY EXPRESSIONS
// =============================================================================

// Parenthesized expression
parenthesizedExpr
    : LEFT_PAREN expression RIGHT_PAREN
    ;

// Dynamic array
// Example: {1. 2. 3}
dynamicArray
    : LEFT_BRACE (expression (PERIOD expression)*)? RIGHT_BRACE
    ;

// =============================================================================
// SELECTORS AND IDENTIFIERS
// =============================================================================

// Unary selector
unarySelector
    : IDENTIFIER
    ;

// Binary selector
binarySelector
    : PLUS | MINUS | STAR | DIVIDE | MOD 
    | INTEGER_DIVIDE | MODULO
    | BIT_SHIFT_LEFT | BIT_SHIFT_RIGHT | BACKSLASH | TILDE | PIPE | BIT_AND
    | EQUAL | NOT_EQUAL | EQUAL_EQUAL | NOT_IDENTITY
    | LT | LEQ | GT | GEQ
    | AT | QUESTION | COMMA
    ;

// Variables and pseudo-variables
variable
    : IDENTIFIER
    ;

// =============================================================================
// LITERALS
// =============================================================================

// All literal types
literal
    : numberLiteral
    | stringConst
    | symbolConst
    | arrayLiteral
    | byteArrayLiteral
    | characterLiteral
    ;

// Number literals
numberLiteral
    : NUMBER
    ;

// String constant
stringConst
    : STRING
    ;

// Character literal
// Example: $a, $!, $$
characterLiteral
    : CHAR_LITERAL
    ;

// Byte array literal
// Example: #[1 2 255]
byteArrayLiteral
    : HASH LEFT_BRACKET NUMBER* RIGHT_BRACKET
    ;

// Literal array
// Example: #(1 'two' #three)
arrayLiteral
    : HASH LEFT_PAREN arrayElement* RIGHT_PAREN
    ;

// Array element
arrayElement
    : literal
    | MINUS numberLiteral  // Support negative numbers in arrays
    | symbolName
    | LEFT_PAREN arrayElement* RIGHT_PAREN
    ;

// Symbol constant
// Example: #size, #'complex symbol'
symbolConst
    : QUOTED_SYMBOL 
    | HASH symbolName
    ;

// Symbol name variants
symbolName
    : IDENTIFIER
    | unarySelector
    | binarySelector
    | keywordSymbolSelector
    ;

// Keyword symbol
// Example: #at:put:
keywordSymbolSelector
    : (IDENTIFIER COLON)+
    ;

// =============================================================================
// UTILITY RULES
// =============================================================================

// Separator chunk for file-out format
// Example: "-- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- "!
separatorChunk
    : SEPARATOR_LINE BANG
    ;

// Class name  
className
    : IDENTIFIER
    ;

// Superclass name
superclassName
    : IDENTIFIER
    ;

// Argument name
argumentName
    : IDENTIFIER
    ;

// Identifier with colon
identifierColon
    : IDENTIFIER COLON
    ;

// =============================================================================
// LEXER RULES
// =============================================================================
// Order matters! More specific patterns must come before general ones

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
HASH: '#';
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
    : '<' WS_INLINE* 'primitive:' WS_INLINE* DIGITS WS_INLINE* '>'
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
// Symbols
// -----------------------------------------------------------------------------
QUOTED_SYMBOL
    : HASH '\'' ( '\'\'' | ~'\'')* '\''
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
// Fragments
// -----------------------------------------------------------------------------
fragment LETTER: [A-Za-z];
fragment DIGIT: [0-9];
fragment BASEDIGIT: [0-9A-Za-z];
fragment WS_INLINE: [ \t\f];
DIGITS: DIGIT+;

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
// END OF GRAMMAR
// =============================================================================