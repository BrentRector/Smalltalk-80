grammar Smalltalk80;

//////////////////////////////////////////////////////////////////////////
// Parser Entry Points //////////////////////////////////////////////////////////////////////

fileIn: chunk* EOF;

replUnit:
	classDefinition BANG EOF						# ReplClassDef
	| methodCategoryHeader methodDefinition+ EOF	# ReplMethodsForCategory
	| classComment EOF								# ReplClassComment
	| sequence EOF									# ReplSequence;

//////////////////////////////////////////////////////////////////////////
// File-Out Chunks //////////////////////////////////////////////////////////////////////

chunk:
	COMMENT BANG				# StandaloneCommentChunk
	| classDefinition BANG		# ClassChunk
	| classComment BANG			# ClassCommentChunk
	| metaclassDefinition BANG	# MetaClassChunk
	| sequence BANG				# ScriptChunk
	| {
        InputStream.LA(2) == Smalltalk80Parser.UNARY_SELECTOR &&
        (
            InputStream.LA(3) == Smalltalk80Parser.METHODS_FOR ||
            (InputStream.LA(3) == Smalltalk80Parser.CLASS &&
             InputStream.LA(4) == Smalltalk80Parser.METHODS_FOR)
        )
    }? methodCategoryHeader methodDefinitionWithBang+ BANG # MethodCategoryChunk;

methodDefinitionWithBang: methodDefinition BANG;

//////////////////////////////////////////////////////////////////////////
// Class Definitions & Comments
// ////////////////////////////////////////////////////////////////////////

classDefinition:
	UNARY_SELECTOR (
		'subclass:'
		| 'variableSubclass:'
		| 'variableByteSubclass:'
		| 'variableWordSubclass:'
	) HASH UNARY_SELECTOR (classOption)*;

classComment: className CLASS? 'comment:' stringLiteral;

className: UNARY_SELECTOR;

classOption:
	INSTANCE_VARIABLE_NAMES stringLiteral
	| CLASS_VARIABLE_NAMES stringLiteral
	| POOL_DICTIONARIES stringLiteral
	| CATEGORY stringLiteral;

metaclassDefinition:
	UNARY_SELECTOR CLASS INSTANCE_VARIABLE_NAMES stringLiteral;

//////////////////////////////////////////////////////////////////////////
// Method Categories & Definitions
// ////////////////////////////////////////////////////////////////////////

methodCategoryHeader:
	BANG UNARY_SELECTOR CLASS? METHODS_FOR stringLiteral BANG;

methodDefinition: methodSelector COMMENT* sequence;

methodSelector:
        unarySelector
        | binarySelector UNARY_SELECTOR
        | keywordSelector;

unarySelector: UNARY_SELECTOR;

binarySelector: BINARY_SELECTOR | MINUS;

keywordSelector: keywordPart+;

keywordPart: KEYWORD UNARY_SELECTOR;

//////////////////////////////////////////////////////////////////////////
// Sequences (REPL & Scripts)
// ////////////////////////////////////////////////////////////////////////

sequence: temps? expressions;

statement: answer | expression;

temps: PIPE UNARY_SELECTOR (UNARY_SELECTOR)* PIPE;

//////////////////////////////////////////////////////////////////////////
// Statements & Expressions ////////////////////////////////////////////////////////////////////

answer: CARROT expression PERIOD?;

cascade: SEMI_COLON messageSend+;

expressions: expression (PERIOD expression)*;

expression: keywordExpr cascade*;

keywordExpr: binaryExpr (keywordMessage)*;

binaryExpr: unaryExpr (binaryMessage)*;

unaryExpr: primaryExpression (unaryMessage)*;

primaryExpression: assignment | primitive | operand;

messageSend: keywordMessage | binaryMessage | unaryMessage;

unaryMessage: UNARY_SELECTOR;

binaryMessage: (BINARY_SELECTOR | MINUS) unaryExpr;

keywordMessage: KEYWORD binaryExpr (KEYWORD binaryExpr)*;

assignment: variable ASSIGNMENT expression;

//////////////////////////////////////////////////////////////////////////
// Operands & Literals //////////////////////////////////////////////////////////////////////

operand: literal | reference | subexpression;

subexpression: OPEN_PAREN expression CLOSE_PAREN;

variable: UNARY_SELECTOR;

reference: variable;

literal: runtimeLiteral | parsetimeLiteral;

runtimeLiteral: dynamicDictionary | dynamicArray | block;

block:
	BLOCK_START BLOCK_END
	| BLOCK_START nonEmptyBlockBody BLOCK_END;

nonEmptyBlockBody: blockParamList sequence? | temps sequence;

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
        UNARY_SELECTOR
        | BINARY_SELECTOR
        | KEYWORD+
        | stringLiteral;

literalArray: LITARR_START literalArrayRest;

literalArrayRest:
	(parsetimeLiteral | bareLiteralArray | bareSymbol)* CLOSE_PAREN;

bareLiteralArray: OPEN_PAREN literalArrayRest;

primitive: LT KEYWORD DIGIT+ GT;

//////////////////////////////////////////////////////////////////////////
// Lexer Rules //////////////////////////////////////////////////////////////////////

CLASS: 'class';
METHODS_FOR: 'methodsFor:';
INSTANCE_VARIABLE_NAMES: 'instanceVariableNames:';
CLASS_VARIABLE_NAMES: 'classVariableNames:';
POOL_DICTIONARIES: 'poolDictionaries:';
CATEGORY: 'category:';

// KEYWORD must appear before UNARY_SELECTOR so that ifTrue: is matched as a KEYWORD, not a UNARY_SELECTOR.
KEYWORD: [a-zA-Z] [a-zA-Z0-9_]* COLON;
UNARY_SELECTOR: [a-zA-Z] [a-zA-Z0-9_]*;

SEPARATOR: [ \t\r\n\u000C]+ -> skip;
COMMENT: '"' ( ~["] | '""')* '"';

STRING: '\'' ( ~['] | '\'\'')* '\'';

BLOCK_START: '[';
BLOCK_END: ']';
OPEN_PAREN: '(';
CLOSE_PAREN: ')';
PIPE: '|';
PERIOD: '.';
SEMI_COLON: ';';
METHOD_END: '!!';
BANG: '!';
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
IDENTIFIER: [a-zA-Z] [a-zA-Z0-9_]*;
CHARACTER_CONSTANT: '$' ~[\r\n];
DIGIT: [0-9];
HEXDIGIT: [0-9a-fA-F];
RADIX_INT: '-'? DIGIT+ 'r' [0-9A-Za-z]+;

BLOCK_PARAM: COLON IDENTIFIER;
COLON: ':';
ASSIGNMENT: ':=' | '_';
DOLLAR: '$';