grammar Smalltalk80;

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
methodDefinitionWithBang:
	methodSelector sequence BANG // non-empty method body + bang
	| methodSelector BANG ; // selector with zero-body + bang

// Unused: legacy form of a method definition terminated by '!'.
methodDefinitionFile: methodSelector BANG;

////////////////
// REPL Rules // /////////////

// Header for a method category entered interactively in the REPL.
methodCategoryHeaderRepl:
	IDENTIFIER CLASS? METHODS_FOR stringLiteral;

// Legacy rule - no longer referenced by the grammar.
methodDefinitionRepl:
	methodSelector primitive? sequence
	| methodSelector primitive
	| methodSelector;

/////////////////////////////////////////////
// Class & Metaclass Definitions + Comments ///////////////////////////////////////////

classDefinition:
	IDENTIFIER (
		'subclass:'
		| 'variableSubclass:'
		| 'variableByteSubclass:'
		| 'variableWordSubclass:'
	) UNQUOTED_SYMBOL_SIMPLE (classOption)*;

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

//////////////////////////////////////////////////
// 3) Base methodDefinition for REPL (no bangs) // ///////////////////////////////////////////////

// A method can be one of the following forms:
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

keywordPart: (KEYWORD | COMMENT_KW) IDENTIFIER;

//////////////////////////////////////////////////////////////
// 5) sequence: optional primitive placed before statements //
// ///////////////////////////////////////////////////////////

// A "sequence" may declare temporaries and optionally include a primitive. At least one of
// temporaries, primitive, or statements must be present to avoid ambiguity when parsing empty
// method bodies.
sequence:
	temps primitive? statements? // declares temps, then optional prim/stmts
	| primitive statements? // a primitive (and optional stmts)
	| statements; // one or more statements

//////////////////////////////////////////////////////////////////////////
// 6) statements: support multiple dots, but force ≥1 stmt //
// ////////////////////////////////////////////////////////////////////////

// We swallow runs of periods as separators, but we always consume at least one statement up front.
// • statement – first, required • ( PERIOD+ statement )* – zero or more “. stmt” • PERIOD* – any
// trailing dots
statements: statement ( PERIOD+ statement)* PERIOD*;

statement:
	answer // ^x
	| expression; // variable assignments, cascades, etc.

/////////////////////////////
// Statements & Expressions ///////////////////////////

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

cascadeMessage:
	';' (
		// any full message form:
		unaryExpression // e.g. foo
		| unaryExpression (
			(BINARY_SELECTOR | PIPE | MINUS) unaryExpression
		) // e.g. + bar
		| KEYWORD binaryExpression (
			(KEYWORD | COMMENT_KW) binaryExpression
		)* // e.g. show: arg
		| keywordExpression // e.g. add: x to: y
	);

unaryMessage: IDENTIFIER | CLASS;

primaryExpression: assignment | operand | returnExpr;

assignment: variable ASSIGNMENT expression;

/////////////////////////
// Operands & Literals // ///////////////////////

operand: literal | reference | subexpression | block;

subexpression: OPEN_PAREN expression CLOSE_PAREN;

variable: IDENTIFIER | CLASS;

reference: variable;

literal: runtimeLiteral | parsetimeLiteral;

runtimeLiteral: dynamicDictionary | dynamicArray | block;

// Smalltalk Block Syntax: Parameters and Temporaries In Smalltalk-80 a block can declare both
// formal parameters and temporary variables. The order is always: Zero or more block parameters,
// each prefixed by : A single | to close the parameter list Zero or more temporaries, enclosed by
// |…| The block’s body expressions The closing ]

// Example Forms No params, no temps: [ doSomething ]
// 
// Only parameters: [ :x :y doSomethingWith: x and: y ]
// 
// Only temporaries: [ | a b | a := compute. b := adjust. ^a + b ]
// 
// Both params and temps: [ :x :y | temp1 temp2 | temp1 := f(x). temp2 := g(y). ^temp1 + temp2 ]

// Classic Smalltalk lets you omit the closing | of the parameter list when you don’t have any
// temporaries. In other words, both these are legal: [ :x :y | x + y ] “params + pipe + no temps +
// body” [ :x :y x + y ] “params + no pipe + body”

// 1) params: “:x :y |” or “:x :y” (pipe optional if no temps follow)
blockParamList: (COLON (IDENTIFIER | CLASS))+;

// 2) temps: “| a b |”
temps: PIPE (IDENTIFIER | CLASS)+ PIPE;

// 3) body of a block  
blockStatements: statement ( PERIOD+ statement)* PERIOD?;

// 4) the block itself
block:
	BLOCK_START (
		// params (with optional trailing pipe if no temps follow)
		blockParamList PIPE? temps?
		| // temps only
		temps
		| // no header
		/* ε */
	) blockStatements? BLOCK_END;

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

symbol
  : UNQUOTED_SYMBOL_KEYWORD   // e.g. #foo:bar:baz:
  | UNQUOTED_SYMBOL_SIMPLE    // e.g. #foo
  | HASH STRING               // e.g. #'any text—even with # inside'
  ;


number: numberExp | hex_ | stFloat | stInteger | RADIX_INT;

numberExp: (stFloat | stInteger) EXP stInteger;

charConstant: CHARACTER_CONSTANT;

hex_: MINUS? HEX HEXDIGIT+;

stInteger: MINUS? DIGIT+;

stFloat: MINUS? DIGIT+ PERIOD DIGIT+;

pseudoVariable: RESERVED_WORD;

stringLiteral: STRING;

literalArray: LITARR_START literalArrayRest;

literalArrayRest: (parsetimeLiteral | bareLiteralArray | symbol)* CLOSE_PAREN;

bareLiteralArray: OPEN_PAREN literalArrayRest;

primitive: PRIMITIVE;

////////////////
// Lexer Rules //////////////

// 1a) multi‐keyword unquoted symbols, no spaces:  #foo:bar:baz:
UNQUOTED_SYMBOL_KEYWORD
  : '#' [A-Za-z][A-Za-z0-9_]* (':' [A-Za-z][A-Za-z0-9_]*)+ 
  ;

// 1b) simple unquoted symbols:  #foo or #HierarchyViolation
UNQUOTED_SYMBOL_SIMPLE
  : '#' [A-Za-z][A-Za-z0-9_]* 
  ;

// then your normal STRING rule (which already lets you embed # safely)
STRING
  : '\'' (~['] | '\'\'' )* '\''
  ;


//
// The above must precede the KEYWORD and IDENTIFIER rules.
// 
//

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


// all inline comments vanish into HIDDEN:
COMMENT: '"' ( ~["] | '""')* '"' -> channel(HIDDEN);

// whitespace
SEPARATOR: [ \t\r\n\u000C]+ -> skip;

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
COLON: ':';
ASSIGNMENT: ':=' | '_';
DOLLAR: '$';