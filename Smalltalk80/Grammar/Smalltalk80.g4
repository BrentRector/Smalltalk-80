grammar Smalltalk80;

// Unused parser rules: <c>methodDefinitionFile</c> and <c>methodDefinitionRepl</c> remain for
// historical reference. Unused lexer tokens: <c>LT</c>, <c>GT</c> and <c>DOLLAR</c>.

//////////////////////////////////////////////////////////////////////////
// Parser Entry Points ////////////////////////////////////////////////////////////////////////

// Top-level rule for parsing classic file-out sources comprised of one or more <c>chunk</c>
// elements followed by the end of file.
fileIn: chunk* EOF;

//////////////////////////////////////////////////////////////////////////
// REPL entry point (no bangs, at least one method after header)
// ////////////////////////////////////////////////////////////////////////

// In REPL mode a sequence of definitions is parsed without bang delimiters. At least one
// <c>methodDefinition</c> is required when parsing a method category.
replUnit:
	classDefinition EOF									# ReplClassDef
	| methodCategoryHeaderRepl methodDefinition+ EOF	# ReplMethodsForCategory
	| classComment EOF									# ReplClassComment
	| sequence EOF										# ReplSequence;

//////////////////////////////////////////////////////////////////////////
// File-in Rules //////////////////////////////////////////////////////////
// ////////////////////////////////////////////////////////////////////////

// A <c>chunk</c> represents one logical unit in a classic file-out. Chunks may be standalone
// comments, class definitions, or sequences of methods terminated by bangs.
chunk:
	BANG						# StandaloneCommentChunk
	| classDefinition BANG		# ClassChunk
	| classComment BANG			# ClassCommentChunk
	| metaclassDefinition BANG	# MetaClassChunk
	| sequence BANG				# ScriptChunk
	| {
        InputStream.LA(2) == Smalltalk80Parser.IDENTIFIER &&
        (
            InputStream.LA(3) == Smalltalk80Parser.METHODS_FOR ||
            (InputStream.LA(3) == Smalltalk80Parser.CLASS &&
             InputStream.LA(4) == Smalltalk80Parser.METHODS_FOR)
        )
      }? methodCategoryHeaderFile // header: …methodsFor: '…'!
	methodDefinitionWithBang+ // one or more methods, each ending in '!'
	BANG // final '!' closes the entire category
	# MethodCategoryChunk;

// Header for a file-in method category. Delimited by bangs on either side.
methodCategoryHeaderFile:
	BANG IDENTIFIER CLASS? METHODS_FOR stringLiteral BANG;

// Definition of a method that must end in a bang. The actual method body is parsed using
// <c>methodDefinition</c> to allow optional primitives and statements.
methodDefinitionWithBang: methodDefinition BANG;

// Unused: legacy form of a method definition terminated by '!'.
methodDefinitionFile: methodSelector BANG;

//////////////////////////////////////////////////////////////////////////
// REPL Rules /////////////////////////////////////////////////////////////
// ////////////////////////////////////////////////////////////////////////

// Header for a method category entered interactively in the REPL.
methodCategoryHeaderRepl:
	IDENTIFIER CLASS? METHODS_FOR stringLiteral;

// Legacy rule - no longer referenced by the grammar.
methodDefinitionRepl:
	methodSelector primitive? sequence
	| methodSelector primitive
	| methodSelector;

//////////////////////////////////////////////////////////////////////////
// Class & Metaclass Definitions + Comments
// ////////////////////////////////////////////////////////////////////////

classDefinition:
	IDENTIFIER (
		'subclass:'
		| 'variableSubclass:'
		| 'variableByteSubclass:'
		| 'variableWordSubclass:'
	) HASH IDENTIFIER (classOption)*;

classComment: className CLASS? 'comment:' stringLiteral;

className: IDENTIFIER;

classOption:
	INSTANCE_VARIABLE_NAMES stringLiteral
	| CLASS_VARIABLE_NAMES stringLiteral
	| POOL_DICTIONARIES stringLiteral
	| CATEGORY stringLiteral;

metaclassDefinition:
	IDENTIFIER CLASS INSTANCE_VARIABLE_NAMES stringLiteral;

//////////////////////////////////////////////////////////////////////////
// 3) Base methodDefinition for REPL (no bangs) //
// ////////////////////////////////////////////////////////////////////////

// A method can be one of the following forms:
// 
// • selector + optional primitive + body • selector + primitive only • selector alone
methodDefinition:
	methodSelector primitive? sequence // primitive + optional code
	| methodSelector primitive // primitive only
	| methodSelector; // empty body

methodSelector:
	unarySelector
	| binarySelector IDENTIFIER
	| keywordSelector;

unarySelector: IDENTIFIER;

binarySelector: BINARY_SELECTOR | MINUS;

keywordSelector: keywordPart+;

keywordPart: KEYWORD IDENTIFIER;

//////////////////////////////////////////////////////////////////////////
// 5) sequence: require at least one real statement //
// ////////////////////////////////////////////////////////////////////////

// A “sequence” may start with temporaries, but it must contain at least one statement before it can
// end.
sequence: temps? statements;

//////////////////////////////////////////////////////////////////////////
// 6) statements: support multiple dots, but force ≥1 stmt //
// ////////////////////////////////////////////////////////////////////////

// We swallow runs of periods as separators, but we always consume at least one statement up front.
// 
//
// 
//
// • statement – first, required • ( PERIOD+ statement )* – zero or more “. stmt” • PERIOD* – any
// trailing dots
statements: statement ( PERIOD+ statement)* PERIOD*;

statement: primitive | answer | expression;

temps: PIPE variable+ PIPE;

//////////////////////////////////////////////////////////////////////////
// Statements & Expressions ////////////////////////////////////////////////////////////////////////

returnExpr: CARROT expression;

answer: returnExpr PERIOD?;

cascade: SEMI_COLON messageSend+;

expressions: expression (PERIOD expression)*;

expression: keywordExpr cascade*;

keywordExpr: binaryExpr (KEYWORD expression)*;

binaryExpr: unaryExpr (binaryMessage)*;

unaryExpr: primaryExpression (unaryMessage)*;

primaryExpression: assignment | operand | returnExpr;

messageSend:
	unaryMessage
	| binaryMessage
	| (KEYWORD expression)+;

unaryMessage: IDENTIFIER | CLASS;

binaryMessage: (BINARY_SELECTOR | MINUS) unaryExpr;

assignment: variable ASSIGNMENT expression;

//////////////////////////////////////////////////////////////////////////
// Operands & Literals ////////////////////////////////////////////////////////////////////////

operand: literal | reference | subexpression | block;

subexpression: OPEN_PAREN expression CLOSE_PAREN;

variable: IDENTIFIER | CLASS;

reference: variable;

literal: runtimeLiteral | parsetimeLiteral;

runtimeLiteral: dynamicDictionary | dynamicArray | block;

block:
	BLOCK_START BLOCK_END
	| BLOCK_START blockParamList (PIPE temps? statements)? BLOCK_END
	| BLOCK_START temps? statements BLOCK_END;

blockParamList: BLOCK_PARAM+;

dynamicDictionary: DYNDICT_START expressions? DYNARR_END;

dynamicArray: DYNARR_START expressions? DYNARR_END;

parsetimeLiteral:
	pseudoVariable
	| number
	| charConstant
	| literalArray
	| stringLiteral
	| symbol;

number: numberExp | hex_ | stFloat | stInteger | RADIX_INT;

numberExp: (stFloat | stInteger) EXP stInteger;

charConstant: CHARACTER_CONSTANT;

hex_: MINUS? HEX HEXDIGIT+;

stInteger: MINUS? DIGIT+;

stFloat: MINUS? DIGIT+ PERIOD DIGIT+;

pseudoVariable: RESERVED_WORD;

stringLiteral: STRING;

symbol: HASH bareSymbol;

bareSymbol:
	IDENTIFIER
	| BINARY_SELECTOR
	| KEYWORD+
	| stringLiteral;

literalArray: LITARR_START literalArrayRest;

literalArrayRest: (
		parsetimeLiteral
		| bareLiteralArray
		| bareSymbol
	)* CLOSE_PAREN;

bareLiteralArray: OPEN_PAREN literalArrayRest;

primitive: PRIMITIVE;

//////////////////////////////////////////////////////////////////////////
// Lexer Rules ////////////////////////////////////////////////////////////////////////

CLASS: 'class';
METHODS_FOR: 'methodsFor:';
INSTANCE_VARIABLE_NAMES: 'instanceVariableNames:';
CLASS_VARIABLE_NAMES: 'classVariableNames:';
POOL_DICTIONARIES: 'poolDictionaries:';
CATEGORY: 'category:';

// KEYWORD must appear before IDENTIFIER so that ifTrue: → KEYWORD
KEYWORD: [a-zA-Z] [a-zA-Z0-9_]* COLON;
IDENTIFIER: [a-zA-Z] [a-zA-Z0-9_]*;

// whitespace
SEPARATOR: [ \t\r\n\u000C]+ -> skip;

// all inline comments vanish into HIDDEN:
COMMENT: '"' ( ~["] | '""')* '"' -> channel(HIDDEN);

STRING: '\'' ( ~['] | '\'\'')* '\'';
BLOCK_START: '[';
BLOCK_END: ']';
OPEN_PAREN: '(';
CLOSE_PAREN: ')';
PIPE: '|';
PERIOD: '.';
SEMI_COLON: ';';
BANG: '!';
fragment SP: (' ' | '\t')+;
PRIMITIVE: '<primitive:' SP? DIGIT+ '>';
MINUS: '-';
BINARY_SELECTOR: (
		'\\'
		| '+'
		| '*'
		| '/'
		| '='
		| '>'
		| '<'
		| ','
		| '@'
		| '%'
		| '~'
		| '|'
		| '&'
		| '?'
	)+;
// The following tokens are defined for completeness but are not referenced in the current parser
// rules.
LT: '<';
GT: '>';
CARROT: '^';
HASH: '#';
EXP: 'e';
HEX: '16r';
LITARR_START: '#(';
DYNDICT_START: '#{';
DYNARR_END: '}';
DYNARR_START: '{';
RESERVED_WORD: 'nil' | 'true' | 'false' | 'self' | 'super';
CHARACTER_CONSTANT: '$' ~[\r\n];
DIGIT: [0-9];
HEXDIGIT: [0-9a-fA-F];
RADIX_INT: '-'? DIGIT+ 'r' [0-9A-Za-z]+;
BLOCK_PARAM: ':' [ \t\r\n\u000C]* [a-zA-Z] [a-zA-Z0-9_]*;
COLON: ':';
ASSIGNMENT: ':=' | '_';
DOLLAR: '$';