grammar Smalltalk80;

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

// ─────────────────────────────────────────────────────────────── Entry Points
// ───────────────────────────────────────────────────────────────

fileIn: chunk* EOF; // file-out style with ! terminators
expressionEntry: expression EOF; // REPL expression input
statementsEntry: statements EOF; // Workspace or do-it block
methodEntry:
	methodHeader temporaries? primitiveDecl? methodBodyNoBang EOF;
classEntry: classHeader classBody EOF; // Class editor input

// ─────────────────────────────────────────────────────────────── Chunks (file-in mode)
// ───────────────────────────────────────────────────────────────

chunk:
        metaclassCommentChunk
        | metaclassDefinitionChunk
        | classCommentChunk
        | classDefChunk
        | separatorChunk
        | expressionChunk
        | methodCategoryChunk
        | BANG;

expressionChunk: expression BANG;

classDefChunk: classHeader classBody BANG;

classHeader: className subclassKind symbolConst;

subclassKind
    : kind=identifierColon
      {
          var text = $kind.text;
          if (text is not ("subclass:" or "variableSubclass:" or "variableByteSubclass:" or "variableWordSubclass:"))
              NotifyErrorListeners($"invalid subclass kind: {text}");
      }
    ;

primitiveDecl: PRIMITIVE_DECL;

classBody: classSlotSpec* categoryDecl;

classSlotSpec
    : spec=identifierColon stringConst
      {
          var text = $spec.text;
          if (text is not (
                  "instanceVariableNames:" or
                  "classVariableNames:" or
                  "classInstanceVariableNames:" or
                  "poolDictionaries:"))
              NotifyErrorListeners($"invalid slot specification: {text}");
      }
    ;

categoryDecl
    : kw=identifierColon stringConst
      {
          if ($kw.text != "category:")
              NotifyErrorListeners("expected 'category:'");
      }
    ;

// ─────────────────────────────────────────────────────────────── Comments & Metadata
// ───────────────────────────────────────────────────────────────

classCommentChunk
    : { NextMatchesKeyword(2, "comment") }?
      className kw=identifierColon NEW_LINE? stringConst BANG
      {
          if ($kw.text != "comment:")
              NotifyErrorListeners("expected 'comment:'");
      }
    ;

metaclassDefinitionChunk
    : { LooksLikeMetaclassDef() }?
      className cls=IDENTIFIER
          { if ($cls.text != "class") NotifyErrorListeners("expected 'class'"); }
      classSlotSpec+ BANG;

metaclassCommentChunk
    : { NextMatchesKeyword(3, "comment") }?
      className cls=IDENTIFIER
          { if ($cls.text != "class") NotifyErrorListeners("expected 'class'"); }
      kw=identifierColon NEW_LINE? stringConst BANG
      {
          if ($kw.text != "comment:")
              NotifyErrorListeners("expected 'comment:'");
      }
    ;

// ─────────────────────────────────────────────────────────────── Method Definitions
// ───────────────────────────────────────────────────────────────

methodDef:
	methodHeader stringConst? NEW_LINE? temporaries? primitiveDecl? methodBody;

methodBody:
	statements PERIOD* BANG // standard method
	| returnExpr BANG // short form return
	| BANG; // empty method

methodBodyNoBang:
	statements PERIOD* // REPL-compatible
	| returnExpr; // ^expr (no bang)

methodCategoryChunk
    : {IsMethodsFor()}?
      BANG className (cls=IDENTIFIER { if ($cls.text != "class") NotifyErrorListeners("expected 'class'"); })?
      kw=identifierColon stringConst BANG (
                methodDef
        )* BANG
      {
          if ($kw.text != "methodsFor:")
              NotifyErrorListeners("expected 'methodsFor:'");
      }
    ;

methodHeader
    : unarySelector
    | binarySelector formalArg
    | (identifierColon formalArg)+
    ;

formalArg: argumentName;

temporaries: PIPE temporaryName+ PIPE NEW_LINE?;
temporaryName: IDENTIFIER;
// ─────────────────────────────────────────────────────────────── Statements & Expressions
// ───────────────────────────────────────────────────────────────

statements: statement (PERIOD+ statement)* PERIOD*;

//🔧 statement: handles return and expression only
statement: answer | expression;

answer: returnExpr;

returnExpr: CARAT expression PERIOD?;

// ─────────────────────────────────────────────────────────────── Expression Parsing with Operator
// Precedence ───────────────────────────────────────────────────────────────

// Expressions may be simple arithmetic, message sends, assignments, or a return expression used by
// do-it blocks and workspace evaluations.

//The precedence stack:
/* expression
  → baseExpression
    → returnExpr | assignmentExpr | messageExpr | comparisonExpr
      → lowPrecedenceExpr
        → mediumPrecedenceExpr
          → highPrecedenceExpr */

// 🔧 expression: full expression, possibly with cascade
expression: baseExpression (SEMICOLON messagePart)*;

// 🔧 baseExpression: no cascade, just real expressions
baseExpression:
	returnExpr
	| assignmentExpr
	| messageExpr
	| comparisonExpr;

messagePart: unaryMessage | binaryMessage | keywordMessage;

assignmentExpr: lowPrecedenceExpr ASSIGN expression;

comparisonExpr:
    lowPrecedenceExpr ((EQUAL | NOT_EQUAL | EQUAL_EQUAL | NOT_IDENTITY | LT | LEQ | GT | GEQ) lowPrecedenceExpr)*;

lowPrecedenceExpr:
	mediumPrecedenceExpr ((PLUS | MINUS) mediumPrecedenceExpr)*;

mediumPrecedenceExpr:
	highPrecedenceExpr ((STAR | DIVIDE | MOD) highPrecedenceExpr)*;

highPrecedenceExpr: primaryExpr | prefixExpr;

prefixExpr: (MINUS | TILDE) primaryExpr;

messageBase:
	parenthesizedExpr
	| returnExpr
	| assignmentExpr
	| lowPrecedenceExpr;

// A message expression consists of a primary receiver followed by zero or more unary, binary, or
// keyword messages. This aligns with the full Smalltalk message syntax while remaining simple for
// this prototype grammar.
messageExpr:
	messageBase (unaryMessage | binaryMessage | keywordMessage)+
	| messageBase;

parenthesizedExpr: '(' expression ')';

primaryExpr: literal | variable | block | '(' expression ')';

// ─────────────────────────────────────────────────────────────── Message Parts
// ───────────────────────────────────────────────────────────────

// Unary messages have no arguments and simply apply a selector to the receiver.
unaryMessage: unarySelector;
binaryMessage: binarySelector expression;
keywordMessage: keywordPart+;

keywordPart: IDENTIFIER COLON expression;

identifierColon: IDENTIFIER COLON;

// ─────────────────────────────────────────────────────────────── Selectors
// ───────────────────────────────────────────────────────────────

// A unary selector is either an identifier (e.g., foo) or one of the special unary messages used by
// the standard library. Supporting identifiers here allows the grammar to parse regular unary
// method definitions and sends such as `increment` or `yourself`.
unarySelector:
        IDENTIFIER;

binarySelector:
	// ───────────── Arithmetic Operators ─────────────
	PLUS // +
	| MINUS // -
	| STAR // *
	| DIVIDE // /
	| MOD // %
	| INTEGER_DIVIDE // //
	| MODULO // \\

	// ───────────── Bitwise Operators ─────────────
        | BIT_SHIFT_LEFT // <<
        | BIT_SHIFT_RIGHT // >>
        | BACKSLASH // \
        | TILDE // ~
        | PIPE // |
        | BIT_AND // &

	// ───────────── Comparison Operators ─────────────
	| EQUAL // =
	| NOT_EQUAL // ~=
	| EQUAL_EQUAL // ==
	| NOT_IDENTITY // ~~
	| LT // <
	| LEQ // <=
	| GT // >
	| GEQ // >=

	// ───────────── Other Symbols & Messaging ─────────────
	| AT // @
	| QUESTION // ?
	| COMMA // ,
	;

primary: literal | variable | block | '(' expression ')';

variable:
        IDENTIFIER
		;

block: '[' blockArgs? blockTemps? statements? ']';

blockArgs: (COLON argumentName)+ PIPE?;
argumentName: IDENTIFIER;

blockTemps: PIPE temporaryName+ PIPE;

literal:
	stringConst
	| numberLiteral
	| symbolConst
	| arrayLiteral
	| byteArrayLiteral
	| characterLiteral;

numberLiteral: NUMBER;

stringConst: STRING;
characterLiteral: CHAR_LITERAL;
byteArrayLiteral: HASH '[' NUMBER* ']';

// ─────────────────────────────────────────────────────────────── Arrays & Symbols
// ───────────────────────────────────────────────────────────────

arrayLiteral: HASH '(' arrayElement* ')';

arrayElement: literal | symbolName | '(' arrayElement* ')';

symbolConst: QUOTED_SYMBOL | HASH (symbolName);

symbolName:
        IDENTIFIER
	| unarySelector
	| binarySelector
	| keywordSymbolSelector;

keywordSymbolSelector: (IDENTIFIER COLON)+;

// ─────────────────────────────────────────────────────────────── Other Chunks
// ───────────────────────────────────────────────────────────────

separatorChunk: '--' (~BANG)* BANG;
className: IDENTIFIER;


// ─────────────────────────────────────────────────────────────── Lexer Rules
// ───────────────────────────────────────────────────────────────


BANG: '!';
COLON: ':';
PERIOD: '.';
ASSIGN: ':=' | '_';
CARAT: '^';
PIPE: '|';
SEMICOLON: ';';
HASH: '#';

PRIMITIVE_DECL:
	'<' WS_INLINE* 'primitive:' WS_INLINE* DIGITS WS_INLINE* '>';

MINUS: '-';
TILDE: '~';

NUMBER:
	DIGITS [rR] BASEDIGIT+ // radix
	| DIGITS ('.' DIGITS)? (('e' | 'E') ('+' | '-')? DIGITS)?; // decimal

COMMENT: '"' ('""' | ~'"')* '"' -> channel(HIDDEN);

STRING: '\'' ( '\'\'' | ~'\'')* '\''; // single-quote strings

QUOTED_SYMBOL: HASH '\'' ( '\'\'' | ~'\'')* '\'';
CHAR_LITERAL
    : '$!!'   		// Escaped bang: treat $!! as a single $!
    | '$' ~[\r\n]; 	// Regular character literal


BIT_SHIFT_LEFT: '<<';
BIT_SHIFT_RIGHT: '>>';
MODULO: '\\\\';
INTEGER_DIVIDE: '//';
EQUAL_EQUAL: '==';
NOT_EQUAL: '~=';
NOT_IDENTITY: '~~';
LEQ: '<=';
GEQ: '>=';
PLUS: '+';
STAR: '*';
DIVIDE: '/';
MOD: '%';
LT: '<';
GT: '>';
EQUAL: '=';
AT: '@';
QUESTION: '?';
BACKSLASH: '\\';
COMMA: ',';
BIT_AND: '&';


IDENTIFIER: LETTER (LETTER | DIGIT)*;

fragment LETTER: [A-Za-z];
fragment DIGIT: [0-9];
fragment BASEDIGIT: [0-9A-Za-z];
fragment WS_INLINE: [ \t\f];
DIGITS: DIGIT+;

NEW_LINE: ('\r'? '\n')+ -> skip;
WS: [ \t\f]+ -> skip;