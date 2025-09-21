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

identifierColon: IDENTIFIER COLON;

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
	| simpleMethodChunk
	| expressionChunk
	| methodCategoryChunk
	| BANG;

simpleMethodChunk: className (cls = IDENTIFIER { if ($cls.text != "class") NotifyErrorListeners("expected 'class'"); })? BIT_SHIFT_RIGHT methodDef;

expressionChunk: expression BANG;

classDefChunk: classHeader classBody BANG;

classHeader: className subclassKind symbolConst;

subclassKind:
	kind = identifierColon {
          var text = $kind.text;
          if (text is not ("subclass:" or "variableSubclass:" or "variableByteSubclass:" or "variableWordSubclass:"))
              NotifyErrorListeners($"invalid subclass kind: {text}");
      };

primitiveDecl: PRIMITIVE_DECL;

classBody: classSlotSpec* categoryDecl;

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

categoryDecl:
	kw = identifierColon stringConst {
          if ($kw.text != "category:")
              NotifyErrorListeners("expected 'category:'");
      };

// ─────────────────────────────────────────────────────────────── Comments & Metadata
// ───────────────────────────────────────────────────────────────

classCommentChunk:
	{ NextMatchesKeyword(2, "comment") }? className kw = identifierColon NEW_LINE? stringConst BANG
		{
          if ($kw.text != "comment:")
              NotifyErrorListeners("expected 'comment:'");
      };

metaclassDefinitionChunk:
	{ LooksLikeMetaclassDef() }? className cls = IDENTIFIER { if ($cls.text != "class") NotifyErrorListeners("expected 'class'"); 
		} classSlotSpec+ BANG;

metaclassCommentChunk:
	{ NextMatchesKeyword(3, "comment") }? className cls = IDENTIFIER { if ($cls.text != "class") NotifyErrorListeners("expected 'class'"); 
		} kw = identifierColon NEW_LINE? stringConst BANG {
          if ($kw.text != "comment:")
              NotifyErrorListeners("expected 'comment:'");
      };

// ─────────────────────────────────────────────────────────────── Method Definitions
// ───────────────────────────────────────────────────────────────

methodDef:
	methodHeader stringConst? NEW_LINE? (temporaries primitiveDecl? | primitiveDecl? temporaries?) methodBody;

methodBody:
	statements PERIOD* BANG // standard method
	| returnStatement BANG // short form return
	| BANG; // empty method

methodBodyNoBang:
	statements PERIOD* // REPL-compatible
	| returnStatement; // ^expr (no bang)

methodCategoryChunk:
	{IsMethodsFor()}? BANG className (
		cls = IDENTIFIER { if ($cls.text != "class") NotifyErrorListeners("expected 'class'"); }
	)? kw = identifierColon stringConst BANG (methodDef)* BANG {
          if ($kw.text != "methodsFor:")
              NotifyErrorListeners("expected 'methodsFor:'");
      };

methodHeader:
	unarySelector
	| binarySelector formalArg
	| (identifierColon formalArg)+;

formalArg: argumentName;

temporaries: PIPE temporaryName+ PIPE NEW_LINE?;
temporaryName: IDENTIFIER;

////
// / Statements //
statements: PERIOD* statement (PERIOD+ statement)* PERIOD*;

statement: returnStatement | expressionStatement;

returnStatement: CARET expression PERIOD?;

expressionStatement: expression;

////
// / Expression structure //
expression: assignmentExpr;

assignmentExpr:
	variable ASSIGN assignmentExpr // right-associative assignment
	| cascadeExpr;

cascadeExpr: messageExpr (SEMICOLON messagePart)*; // cascades

////
// / Message expression //
messageExpr:
	messageBase (unaryMessage | binaryMessage)* keywordMessage?;

messageBase:
	literal
	| parenthesizedExpr
	| block
	| dynamicArray
	| variable
	| negativeNumber;

negativeNumber: MINUS messageBase;

messagePart: unaryMessage | binaryMessage | keywordMessage;

////
// / Messages //
unaryMessage: unarySelector;

binaryMessage: binarySelector messageBase;

keywordMessage: keywordPart+;

keywordPart: IDENTIFIER COLON nonKeywordExpr;

// Non-keyword expressions (for keyword arguments) - no keyword messages allowed
nonKeywordExpr: messageBase (unaryMessage | binaryMessage)*;

////
// / Blocks and Primary Expressions //
block:
	LEFT_BRACKET blockArgs? blockTemps? statements? RIGHT_BRACKET;

blockArgs: (COLON argumentName)+ PIPE?;

argumentName: IDENTIFIER;

blockTemps: PIPE temporaryName+ PIPE;

parenthesizedExpr: LEFT_PAREN expression RIGHT_PAREN;

dynamicArray: LEFT_BRACE (expression (PERIOD expression)*)? RIGHT_BRACE;

////
// / Selectors and Tokens //
unarySelector: IDENTIFIER;

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
	| COMMA; // ,

variable: IDENTIFIER;

literal:
	numberLiteral
	| stringConst
	| symbolConst
	| arrayLiteral
	| byteArrayLiteral
	| characterLiteral;

numberLiteral: NUMBER;
stringConst: STRING;
characterLiteral: CHAR_LITERAL;
byteArrayLiteral: HASH LEFT_BRACKET NUMBER* RIGHT_BRACKET;
arrayLiteral: HASH LEFT_PAREN arrayElement* RIGHT_PAREN;
arrayElement:
	literal
	| symbolName
	| LEFT_PAREN arrayElement* RIGHT_PAREN;
symbolConst: QUOTED_SYMBOL | HASH symbolName;
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

ASSIGN: '_';
BIT_SHIFT_LEFT: '<<';
BIT_SHIFT_RIGHT: '>>';
INTEGER_DIVIDE: '\\\\';
MODULO: '//';
EQUAL_EQUAL: '==';
NOT_IDENTITY: '~~';
LEQ: '<=';
GEQ: '>=';
NOT_EQUAL: '~=';
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
LEFT_BRACKET: '[';
RIGHT_BRACKET: ']';
LEFT_PAREN: '(';
RIGHT_PAREN: ')';
LEFT_BRACE: '{';
RIGHT_BRACE: '}';
SEMICOLON: ';';
PERIOD: '.';

PRIMITIVE_DECL:
	'<' WS_INLINE* 'primitive:' WS_INLINE* DIGITS WS_INLINE* '>';

NUMBER:
	DIGITS [rR] BASEDIGIT+ // radix
	| DIGITS ('.' DIGITS)? (('e' | 'E') ('+' | '-')? DIGITS)?; // decimal

COMMENT: '"' ('""' | ~'"')* '"' -> channel(HIDDEN);

STRING: '\'' ( '\'\'' | ~'\'')* '\''; // single-quote strings

QUOTED_SYMBOL: HASH '\'' ( '\'\'' | ~'\'')* '\'';
CHAR_LITERAL:
	'$!!' // Escaped bang: treat $!! as a single $!
	| '$' ~[\r\n]; // Regular character literal

IDENTIFIER: LETTER (LETTER | DIGIT)*;

fragment LETTER: [A-Za-z];
fragment DIGIT: [0-9];
fragment BASEDIGIT: [0-9A-Za-z];
fragment WS_INLINE: [ \t\f];
DIGITS: DIGIT+;

NEW_LINE: ('\r'? '\n')+ -> skip;
WS: [ \t\f]+ -> skip;
