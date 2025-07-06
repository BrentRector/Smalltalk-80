grammar Smalltalk80;

//////////////////////////////////////////////////////////////////////////
// Parser Entry Points ////////////////////////////////////////////////////////////////////////

fileIn: chunk* EOF;

replUnit:
	classDefinition BANG EOF						# ReplClassDef
	| methodCategoryHeader methodDefinition+ EOF	# ReplMethodsForCategory
	| classComment EOF								# ReplClassComment
	| sequence EOF									# ReplSequence;

//////////////////////////////////////////////////////////////////////////
// File-Out Chunks ////////////////////////////////////////////////////////////////////////

chunk:
	COMMENT BANG				# StandaloneCommentChunk
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
    }? methodCategoryHeader methodDefinitionWithBang+ BANG # MethodCategoryChunk;

methodDefinitionWithBang: methodDefinition BANG;

//////////////////////////////////////////////////////////////////////////
// Class Definitions & Comments
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
// Method Categories & Definitions
// ////////////////////////////////////////////////////////////////////////

methodCategoryHeader:
	BANG IDENTIFIER CLASS? METHODS_FOR stringLiteral BANG;

methodDefinition: methodSelector COMMENT* sequence;

methodSelector:
	unarySelector
	| binarySelector IDENTIFIER
	| keywordSelector;

unarySelector: IDENTIFIER;

binarySelector: BINARY_SELECTOR | MINUS;

keywordSelector: keywordPart+;

keywordPart: KEYWORD IDENTIFIER;

//////////////////////////////////////////////////////////////////////////
// Sequences (REPL & Scripts)
// ////////////////////////////////////////////////////////////////////////

sequence: temps? expressions;

statement: answer | expression;

temps: PIPE IDENTIFIER (IDENTIFIER)* PIPE;

//////////////////////////////////////////////////////////////////////////
// Statements & Expressions ////////////////////////////////////////////////////////////////////////

answer: CARROT expression PERIOD?;

expressions: expression (PERIOD expression)*;

expression:
	expression SEMI_COLON message (SEMI_COLON message)* // multiple cascades
	| expression unaryMessage
	| expression binaryMessage
	| expression keywordMessage
	| assignment
	| primitive
	| operand;

unaryMessage: unarySelector;

binaryMessage: (BINARY_SELECTOR | MINUS) expression;

keywordMessage: keywordPair+;

keywordPair: KEYWORD expression;

assignment: variable ASSIGNMENT expression;

message: binaryMessage | unaryMessage | keywordMessage;

//////////////////////////////////////////////////////////////////////////
// Operands & Literals ////////////////////////////////////////////////////////////////////////

operand: literal | reference | subexpression;

subexpression: OPEN_PAREN expression CLOSE_PAREN;

variable: IDENTIFIER;

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
	IDENTIFIER
	| BINARY_SELECTOR
	| KEYWORD+
	| stringLiteral;

literalArray: LITARR_START literalArrayRest;

literalArrayRest:
	(parsetimeLiteral | bareLiteralArray | bareSymbol)* CLOSE_PAREN;

bareLiteralArray: OPEN_PAREN literalArrayRest;

primitive: LT KEYWORD DIGIT+ GT;

//////////////////////////////////////////////////////////////////////////
// Lexer Rules ////////////////////////////////////////////////////////////////////////

CLASS: 'class';
METHODS_FOR: 'methodsFor:';
INSTANCE_VARIABLE_NAMES: 'instanceVariableNames:';
CLASS_VARIABLE_NAMES: 'classVariableNames:';
POOL_DICTIONARIES: 'poolDictionaries:';
CATEGORY: 'category:';
KEYWORD: IDENTIFIER COLON;

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