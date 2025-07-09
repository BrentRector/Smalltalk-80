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

// 'comment:' is a standard keyword, but we want tO use the dedicated literal token COMMENT_KW. This
// enables the lexer to generate a specialized token helping the visitor to locate standlone class
// comments.
classComment: className CLASS? COMMENT_KW stringLiteral;

className: IDENTIFIER;

classOption:
	INSTANCE_VARIABLE_NAMES stringLiteral
	| CLASS_VARIABLE_NAMES stringLiteral
	| POOL_DICTIONARIES stringLiteral
	| KEYWORD stringLiteral;

metaclassDefinition:
	IDENTIFIER CLASS INSTANCE_VARIABLE_NAMES stringLiteral;

//////////////////////////////////////////////////////////////////////////
// 3) Base methodDefinition for REPL (no bangs) //
// ////////////////////////////////////////////////////////////////////////

// A method can be one of the following forms:
// 
//
// 
//
// 
//
// 
//
// 
//
// 
//
// 
//
// 
//
// 
//
// 
//
// 
//
// 
//
// 
//
// 
//
// 
//
// 
//
methodDefinition:
	methodSelector sequence // body and/or primitive
	| methodSelector; // empty body

methodSelector:
	unarySelector
	| binarySelector IDENTIFIER
	| keywordSelector;

unarySelector: IDENTIFIER | CLASS;

// A binary selector may be comprised of standard binary characters or a single pipe character. The
// pipe token normally denotes temporary variable lists, but here it also serves as the selector for
// Boolean disjunction.
binarySelector: BINARY_SELECTOR | PIPE | MINUS;

keywordSelector: keywordPart+;

keywordPart: KEYWORD IDENTIFIER;

//////////////////////////////////////////////////////////////////////////
// 5) sequence: optional primitive placed before statements //
// ////////////////////////////////////////////////////////////////////////

// A "sequence" may declare temporaries. If a primitive declaration is present it must appear
// immediately after the temporaries and before any statements. Statements are optional when a
// primitive is provided.
sequence: temps? primitive? statements?;

//////////////////////////////////////////////////////////////////////////
// 6) statements: support multiple dots, but force ≥1 stmt //
// ////////////////////////////////////////////////////////////////////////

// We swallow runs of periods as separators, but we always consume at least one statement up front.
// 
//
// 
//
// 
//
// 
//
// 
//
// 
//
// 
//
// 
//
// 
//
// 
//
// 
//
// 
//
// 
//
// 
//
// 
//
// 
//

// • statement – first, required • ( PERIOD+ statement )* – zero or more “. stmt” • PERIOD* – any
// trailing dots
statements: statement ( PERIOD+ statement)* PERIOD*;

statement:
	answer // ^x
	| expression; // variable assignments, cascades, etc.

temps: PIPE variable+ PIPE;

//////////////////////////////////////////////////////////////////////////
// Statements & Expressions ////////////////////////////////////////////////////////////////////////

returnExpr: CARROT expression;

answer: returnExpr PERIOD?;

expressions: (expression (PERIOD expression)*)?;

// 1) group primary + unary messages
unaryExpression: primaryExpression (unaryMessage)*;

// 2) binary operators bind tighter than keywords
binaryExpression:
	unaryExpression (
		(BINARY_SELECTOR | PIPE | MINUS) unaryExpression
	)*;

// 3) keyword messages bind tighter than cascades
keywordExpression:
	binaryExpression ((KEYWORD | COMMENT_KW) binaryExpression)*;

// 4) top-level expression allows cascades
expression: keywordExpression (cascadeMessage)*;

// cascades re-send to the same receiver
cascadeMessage:
	';' (
		unaryMessage
		| (BINARY_SELECTOR | PIPE | MINUS) unaryExpression
		| (KEYWORD | COMMENT_KW) unaryExpression
	);

unaryMessage: IDENTIFIER | CLASS;

primaryExpression: assignment | operand | returnExpr;

assignment: variable ASSIGNMENT expression;

//////////////////////////////////////////////////////////////////////////
// Operands & Literals //////////////////////////////////////////////////////////////////////

operand: literal | reference | subexpression | block;

subexpression: OPEN_PAREN expression CLOSE_PAREN;

variable: IDENTIFIER | CLASS;

reference: variable;

literal: runtimeLiteral | parsetimeLiteral;

runtimeLiteral: dynamicDictionary | dynamicArray | block;

block:
	BLOCK_START (blockParamList PIPE)? // optional “[:arg |” header
	temps? // optional “| t1 t2 |”
	statements? // zero or more statements
	BLOCK_END;

blockParamList: BLOCK_PARAM+;

dynamicArray:
	DYNARR_START (expression (PERIOD expression)*)? DYNARR_END;

dynamicDictionary:
	DYNDICT_START (expression (PERIOD expression)*)? DYNARR_END;

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
	| PIPE
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

// Recognize “comment:” separately so it doesn’t collide with other keywords
COMMENT_KW: 'comment:';

CLASS: 'class';
METHODS_FOR: 'methodsFor:';
INSTANCE_VARIABLE_NAMES: 'instanceVariableNames:';
CLASS_VARIABLE_NAMES: 'classVariableNames:';
POOL_DICTIONARIES: 'poolDictionaries:';

// KEYWORD must appear before IDENTIFIER so that ifTrue: → KEYWORD
KEYWORD: [a-zA-Z] [a-zA-Z0-9_]* COLON;
IDENTIFIER: [a-zA-Z] [a-zA-Z0-9_]*;

// whitespace
SEPARATOR: [ \t\r\n\u000C]+ -> skip;

// STRING: single-quoted literals (must come before COMMENT)
STRING: '\'' ( ~['] | '\'\'')* '\'';
// all inline comments vanish into HIDDEN:
COMMENT: '"' ( ~["] | '""')* '"' -> channel(HIDDEN);

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