// Generated from d:/Users/brent/OneDrive/Src/Smalltalk-80/Smalltalk80/Grammar/Smalltalk80.g4 by ANTLR 4.13.1
import org.antlr.v4.runtime.atn.*;
import org.antlr.v4.runtime.dfa.DFA;
import org.antlr.v4.runtime.*;
import org.antlr.v4.runtime.misc.*;
import org.antlr.v4.runtime.tree.*;
import java.util.List;
import java.util.Iterator;
import java.util.ArrayList;

@SuppressWarnings({"all", "warnings", "unchecked", "unused", "cast", "CheckReturnValue"})
public class Smalltalk80Parser extends Parser {
	static { RuntimeMetaData.checkVersion("4.13.1", RuntimeMetaData.VERSION); }

	protected static final DFA[] _decisionToDFA;
	protected static final PredictionContextCache _sharedContextCache =
		new PredictionContextCache();
	public static final int
		T__0=1, T__1=2, T__2=3, T__3=4, T__4=5, T__5=6, T__6=7, T__7=8, T__8=9, 
		T__9=10, T__10=11, T__11=12, T__12=13, T__13=14, T__14=15, IDENTIFIER=16, 
		NUMBER=17, STRING=18, SYMBOL=19, CHARACTER_CONST=20, WS=21, BINARY_SELECTOR=22, 
		KEYWORD=23, LPAREN=24, RPAREN=25, LBRACK=26, RBRACK=27, PIPE=28, COLON=29, 
		ASSIGN=30, DOT=31, SEMICOLON=32, CARAT=33;
	public static final int
		RULE_program = 0, RULE_expressionList = 1, RULE_expression = 2, RULE_assignment = 3, 
		RULE_returnExpr = 4, RULE_cascade = 5, RULE_message = 6, RULE_unaryMessage = 7, 
		RULE_binaryMessage = 8, RULE_keywordMessage = 9, RULE_primary = 10, RULE_literal = 11, 
		RULE_arrayLiteral = 12, RULE_block = 13, RULE_blockArgs = 14, RULE_blockBody = 15, 
		RULE_methodDefinition = 16, RULE_methodHeader = 17, RULE_methodBody = 18, 
		RULE_primitiveDeclaration = 19, RULE_temporaries = 20, RULE_statements = 21, 
		RULE_classDefinition = 22, RULE_classMethodDictionary = 23, RULE_methodList = 24, 
		RULE_unarySelector = 25, RULE_binarySelector = 26;
	private static String[] makeRuleNames() {
		return new String[] {
			"program", "expressionList", "expression", "assignment", "returnExpr", 
			"cascade", "message", "unaryMessage", "binaryMessage", "keywordMessage", 
			"primary", "literal", "arrayLiteral", "block", "blockArgs", "blockBody", 
			"methodDefinition", "methodHeader", "methodBody", "primitiveDeclaration", 
			"temporaries", "statements", "classDefinition", "classMethodDictionary", 
			"methodList", "unarySelector", "binarySelector"
		};
	}
	public static final String[] ruleNames = makeRuleNames();

	private static String[] makeLiteralNames() {
		return new String[] {
			null, "'self'", "'super'", "'nil'", "'true'", "'false'", "'#'", "'<primitive:'", 
			"'>'", "'Class'", "'subclass:'", "'instanceVariableNames:'", "'classVariableNames:'", 
			"'poolDictionaries:'", "'category:'", "'methodsFor:'", null, null, null, 
			null, null, null, null, null, "'('", "')'", "'['", "']'", "'|'", "':'", 
			"':='", "'.'", "';'", "'^'"
		};
	}
	private static final String[] _LITERAL_NAMES = makeLiteralNames();
	private static String[] makeSymbolicNames() {
		return new String[] {
			null, null, null, null, null, null, null, null, null, null, null, null, 
			null, null, null, null, "IDENTIFIER", "NUMBER", "STRING", "SYMBOL", "CHARACTER_CONST", 
			"WS", "BINARY_SELECTOR", "KEYWORD", "LPAREN", "RPAREN", "LBRACK", "RBRACK", 
			"PIPE", "COLON", "ASSIGN", "DOT", "SEMICOLON", "CARAT"
		};
	}
	private static final String[] _SYMBOLIC_NAMES = makeSymbolicNames();
	public static final Vocabulary VOCABULARY = new VocabularyImpl(_LITERAL_NAMES, _SYMBOLIC_NAMES);

	/**
	 * @deprecated Use {@link #VOCABULARY} instead.
	 */
	@Deprecated
	public static final String[] tokenNames;
	static {
		tokenNames = new String[_SYMBOLIC_NAMES.length];
		for (int i = 0; i < tokenNames.length; i++) {
			tokenNames[i] = VOCABULARY.getLiteralName(i);
			if (tokenNames[i] == null) {
				tokenNames[i] = VOCABULARY.getSymbolicName(i);
			}

			if (tokenNames[i] == null) {
				tokenNames[i] = "<INVALID>";
			}
		}
	}

	@Override
	@Deprecated
	public String[] getTokenNames() {
		return tokenNames;
	}

	@Override

	public Vocabulary getVocabulary() {
		return VOCABULARY;
	}

	@Override
	public String getGrammarFileName() { return "Smalltalk80.g4"; }

	@Override
	public String[] getRuleNames() { return ruleNames; }

	@Override
	public String getSerializedATN() { return _serializedATN; }

	@Override
	public ATN getATN() { return _ATN; }

	public Smalltalk80Parser(TokenStream input) {
		super(input);
		_interp = new ParserATNSimulator(this,_ATN,_decisionToDFA,_sharedContextCache);
	}

	@SuppressWarnings("CheckReturnValue")
	public static class ProgramContext extends ParserRuleContext {
		public TerminalNode EOF() { return getToken(Smalltalk80Parser.EOF, 0); }
		public List<ClassDefinitionContext> classDefinition() {
			return getRuleContexts(ClassDefinitionContext.class);
		}
		public ClassDefinitionContext classDefinition(int i) {
			return getRuleContext(ClassDefinitionContext.class,i);
		}
		public List<MethodDefinitionContext> methodDefinition() {
			return getRuleContexts(MethodDefinitionContext.class);
		}
		public MethodDefinitionContext methodDefinition(int i) {
			return getRuleContext(MethodDefinitionContext.class,i);
		}
		public List<ExpressionListContext> expressionList() {
			return getRuleContexts(ExpressionListContext.class);
		}
		public ExpressionListContext expressionList(int i) {
			return getRuleContext(ExpressionListContext.class,i);
		}
		public ProgramContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_program; }
	}

	public final ProgramContext program() throws RecognitionException {
		ProgramContext _localctx = new ProgramContext(_ctx, getState());
		enterRule(_localctx, 0, RULE_program);
		int _la;
		try {
			enterOuterAlt(_localctx, 1);
			{
			setState(59);
			_errHandler.sync(this);
			_la = _input.LA(1);
			while ((((_la) & ~0x3f) == 0 && ((1L << _la) & 8688435838L) != 0)) {
				{
				setState(57);
				_errHandler.sync(this);
				switch ( getInterpreter().adaptivePredict(_input,0,_ctx) ) {
				case 1:
					{
					setState(54);
					classDefinition();
					}
					break;
				case 2:
					{
					setState(55);
					methodDefinition();
					}
					break;
				case 3:
					{
					setState(56);
					expressionList();
					}
					break;
				}
				}
				setState(61);
				_errHandler.sync(this);
				_la = _input.LA(1);
			}
			setState(62);
			match(EOF);
			}
		}
		catch (RecognitionException re) {
			_localctx.exception = re;
			_errHandler.reportError(this, re);
			_errHandler.recover(this, re);
		}
		finally {
			exitRule();
		}
		return _localctx;
	}

	@SuppressWarnings("CheckReturnValue")
	public static class ExpressionListContext extends ParserRuleContext {
		public List<ExpressionContext> expression() {
			return getRuleContexts(ExpressionContext.class);
		}
		public ExpressionContext expression(int i) {
			return getRuleContext(ExpressionContext.class,i);
		}
		public List<TerminalNode> DOT() { return getTokens(Smalltalk80Parser.DOT); }
		public TerminalNode DOT(int i) {
			return getToken(Smalltalk80Parser.DOT, i);
		}
		public ExpressionListContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_expressionList; }
	}

	public final ExpressionListContext expressionList() throws RecognitionException {
		ExpressionListContext _localctx = new ExpressionListContext(_ctx, getState());
		enterRule(_localctx, 2, RULE_expressionList);
		try {
			int _alt;
			enterOuterAlt(_localctx, 1);
			{
			setState(64);
			expression();
			setState(69);
			_errHandler.sync(this);
			_alt = getInterpreter().adaptivePredict(_input,2,_ctx);
			while ( _alt!=2 && _alt!=org.antlr.v4.runtime.atn.ATN.INVALID_ALT_NUMBER ) {
				if ( _alt==1 ) {
					{
					{
					setState(65);
					match(DOT);
					setState(66);
					expression();
					}
					} 
				}
				setState(71);
				_errHandler.sync(this);
				_alt = getInterpreter().adaptivePredict(_input,2,_ctx);
			}
			}
		}
		catch (RecognitionException re) {
			_localctx.exception = re;
			_errHandler.reportError(this, re);
			_errHandler.recover(this, re);
		}
		finally {
			exitRule();
		}
		return _localctx;
	}

	@SuppressWarnings("CheckReturnValue")
	public static class ExpressionContext extends ParserRuleContext {
		public AssignmentContext assignment() {
			return getRuleContext(AssignmentContext.class,0);
		}
		public ReturnExprContext returnExpr() {
			return getRuleContext(ReturnExprContext.class,0);
		}
		public CascadeContext cascade() {
			return getRuleContext(CascadeContext.class,0);
		}
		public ExpressionContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_expression; }
	}

	public final ExpressionContext expression() throws RecognitionException {
		ExpressionContext _localctx = new ExpressionContext(_ctx, getState());
		enterRule(_localctx, 4, RULE_expression);
		try {
			setState(75);
			_errHandler.sync(this);
			switch ( getInterpreter().adaptivePredict(_input,3,_ctx) ) {
			case 1:
				enterOuterAlt(_localctx, 1);
				{
				setState(72);
				assignment();
				}
				break;
			case 2:
				enterOuterAlt(_localctx, 2);
				{
				setState(73);
				returnExpr();
				}
				break;
			case 3:
				enterOuterAlt(_localctx, 3);
				{
				setState(74);
				cascade();
				}
				break;
			}
		}
		catch (RecognitionException re) {
			_localctx.exception = re;
			_errHandler.reportError(this, re);
			_errHandler.recover(this, re);
		}
		finally {
			exitRule();
		}
		return _localctx;
	}

	@SuppressWarnings("CheckReturnValue")
	public static class AssignmentContext extends ParserRuleContext {
		public TerminalNode IDENTIFIER() { return getToken(Smalltalk80Parser.IDENTIFIER, 0); }
		public TerminalNode ASSIGN() { return getToken(Smalltalk80Parser.ASSIGN, 0); }
		public ExpressionContext expression() {
			return getRuleContext(ExpressionContext.class,0);
		}
		public AssignmentContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_assignment; }
	}

	public final AssignmentContext assignment() throws RecognitionException {
		AssignmentContext _localctx = new AssignmentContext(_ctx, getState());
		enterRule(_localctx, 6, RULE_assignment);
		try {
			enterOuterAlt(_localctx, 1);
			{
			setState(77);
			match(IDENTIFIER);
			setState(78);
			match(ASSIGN);
			setState(79);
			expression();
			}
		}
		catch (RecognitionException re) {
			_localctx.exception = re;
			_errHandler.reportError(this, re);
			_errHandler.recover(this, re);
		}
		finally {
			exitRule();
		}
		return _localctx;
	}

	@SuppressWarnings("CheckReturnValue")
	public static class ReturnExprContext extends ParserRuleContext {
		public TerminalNode CARAT() { return getToken(Smalltalk80Parser.CARAT, 0); }
		public ExpressionContext expression() {
			return getRuleContext(ExpressionContext.class,0);
		}
		public ReturnExprContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_returnExpr; }
	}

	public final ReturnExprContext returnExpr() throws RecognitionException {
		ReturnExprContext _localctx = new ReturnExprContext(_ctx, getState());
		enterRule(_localctx, 8, RULE_returnExpr);
		try {
			enterOuterAlt(_localctx, 1);
			{
			setState(81);
			match(CARAT);
			setState(82);
			expression();
			}
		}
		catch (RecognitionException re) {
			_localctx.exception = re;
			_errHandler.reportError(this, re);
			_errHandler.recover(this, re);
		}
		finally {
			exitRule();
		}
		return _localctx;
	}

	@SuppressWarnings("CheckReturnValue")
	public static class CascadeContext extends ParserRuleContext {
		public List<MessageContext> message() {
			return getRuleContexts(MessageContext.class);
		}
		public MessageContext message(int i) {
			return getRuleContext(MessageContext.class,i);
		}
		public List<TerminalNode> SEMICOLON() { return getTokens(Smalltalk80Parser.SEMICOLON); }
		public TerminalNode SEMICOLON(int i) {
			return getToken(Smalltalk80Parser.SEMICOLON, i);
		}
		public CascadeContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_cascade; }
	}

	public final CascadeContext cascade() throws RecognitionException {
		CascadeContext _localctx = new CascadeContext(_ctx, getState());
		enterRule(_localctx, 10, RULE_cascade);
		int _la;
		try {
			enterOuterAlt(_localctx, 1);
			{
			setState(84);
			message();
			setState(89);
			_errHandler.sync(this);
			_la = _input.LA(1);
			while (_la==SEMICOLON) {
				{
				{
				setState(85);
				match(SEMICOLON);
				setState(86);
				message();
				}
				}
				setState(91);
				_errHandler.sync(this);
				_la = _input.LA(1);
			}
			}
		}
		catch (RecognitionException re) {
			_localctx.exception = re;
			_errHandler.reportError(this, re);
			_errHandler.recover(this, re);
		}
		finally {
			exitRule();
		}
		return _localctx;
	}

	@SuppressWarnings("CheckReturnValue")
	public static class MessageContext extends ParserRuleContext {
		public PrimaryContext primary() {
			return getRuleContext(PrimaryContext.class,0);
		}
		public List<UnaryMessageContext> unaryMessage() {
			return getRuleContexts(UnaryMessageContext.class);
		}
		public UnaryMessageContext unaryMessage(int i) {
			return getRuleContext(UnaryMessageContext.class,i);
		}
		public List<BinaryMessageContext> binaryMessage() {
			return getRuleContexts(BinaryMessageContext.class);
		}
		public BinaryMessageContext binaryMessage(int i) {
			return getRuleContext(BinaryMessageContext.class,i);
		}
		public List<KeywordMessageContext> keywordMessage() {
			return getRuleContexts(KeywordMessageContext.class);
		}
		public KeywordMessageContext keywordMessage(int i) {
			return getRuleContext(KeywordMessageContext.class,i);
		}
		public MessageContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_message; }
	}

	public final MessageContext message() throws RecognitionException {
		MessageContext _localctx = new MessageContext(_ctx, getState());
		enterRule(_localctx, 12, RULE_message);
		try {
			int _alt;
			enterOuterAlt(_localctx, 1);
			{
			setState(92);
			primary();
			setState(98);
			_errHandler.sync(this);
			_alt = getInterpreter().adaptivePredict(_input,6,_ctx);
			while ( _alt!=2 && _alt!=org.antlr.v4.runtime.atn.ATN.INVALID_ALT_NUMBER ) {
				if ( _alt==1 ) {
					{
					setState(96);
					_errHandler.sync(this);
					switch (_input.LA(1)) {
					case IDENTIFIER:
						{
						setState(93);
						unaryMessage();
						}
						break;
					case BINARY_SELECTOR:
						{
						setState(94);
						binaryMessage();
						}
						break;
					case KEYWORD:
						{
						setState(95);
						keywordMessage();
						}
						break;
					default:
						throw new NoViableAltException(this);
					}
					} 
				}
				setState(100);
				_errHandler.sync(this);
				_alt = getInterpreter().adaptivePredict(_input,6,_ctx);
			}
			}
		}
		catch (RecognitionException re) {
			_localctx.exception = re;
			_errHandler.reportError(this, re);
			_errHandler.recover(this, re);
		}
		finally {
			exitRule();
		}
		return _localctx;
	}

	@SuppressWarnings("CheckReturnValue")
	public static class UnaryMessageContext extends ParserRuleContext {
		public TerminalNode IDENTIFIER() { return getToken(Smalltalk80Parser.IDENTIFIER, 0); }
		public UnaryMessageContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_unaryMessage; }
	}

	public final UnaryMessageContext unaryMessage() throws RecognitionException {
		UnaryMessageContext _localctx = new UnaryMessageContext(_ctx, getState());
		enterRule(_localctx, 14, RULE_unaryMessage);
		try {
			enterOuterAlt(_localctx, 1);
			{
			setState(101);
			match(IDENTIFIER);
			}
		}
		catch (RecognitionException re) {
			_localctx.exception = re;
			_errHandler.reportError(this, re);
			_errHandler.recover(this, re);
		}
		finally {
			exitRule();
		}
		return _localctx;
	}

	@SuppressWarnings("CheckReturnValue")
	public static class BinaryMessageContext extends ParserRuleContext {
		public TerminalNode BINARY_SELECTOR() { return getToken(Smalltalk80Parser.BINARY_SELECTOR, 0); }
		public PrimaryContext primary() {
			return getRuleContext(PrimaryContext.class,0);
		}
		public BinaryMessageContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_binaryMessage; }
	}

	public final BinaryMessageContext binaryMessage() throws RecognitionException {
		BinaryMessageContext _localctx = new BinaryMessageContext(_ctx, getState());
		enterRule(_localctx, 16, RULE_binaryMessage);
		try {
			enterOuterAlt(_localctx, 1);
			{
			setState(103);
			match(BINARY_SELECTOR);
			setState(104);
			primary();
			}
		}
		catch (RecognitionException re) {
			_localctx.exception = re;
			_errHandler.reportError(this, re);
			_errHandler.recover(this, re);
		}
		finally {
			exitRule();
		}
		return _localctx;
	}

	@SuppressWarnings("CheckReturnValue")
	public static class KeywordMessageContext extends ParserRuleContext {
		public List<TerminalNode> KEYWORD() { return getTokens(Smalltalk80Parser.KEYWORD); }
		public TerminalNode KEYWORD(int i) {
			return getToken(Smalltalk80Parser.KEYWORD, i);
		}
		public List<PrimaryContext> primary() {
			return getRuleContexts(PrimaryContext.class);
		}
		public PrimaryContext primary(int i) {
			return getRuleContext(PrimaryContext.class,i);
		}
		public KeywordMessageContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_keywordMessage; }
	}

	public final KeywordMessageContext keywordMessage() throws RecognitionException {
		KeywordMessageContext _localctx = new KeywordMessageContext(_ctx, getState());
		enterRule(_localctx, 18, RULE_keywordMessage);
		try {
			int _alt;
			enterOuterAlt(_localctx, 1);
			{
			setState(108); 
			_errHandler.sync(this);
			_alt = 1;
			do {
				switch (_alt) {
				case 1:
					{
					{
					setState(106);
					match(KEYWORD);
					setState(107);
					primary();
					}
					}
					break;
				default:
					throw new NoViableAltException(this);
				}
				setState(110); 
				_errHandler.sync(this);
				_alt = getInterpreter().adaptivePredict(_input,7,_ctx);
			} while ( _alt!=2 && _alt!=org.antlr.v4.runtime.atn.ATN.INVALID_ALT_NUMBER );
			}
		}
		catch (RecognitionException re) {
			_localctx.exception = re;
			_errHandler.reportError(this, re);
			_errHandler.recover(this, re);
		}
		finally {
			exitRule();
		}
		return _localctx;
	}

	@SuppressWarnings("CheckReturnValue")
	public static class PrimaryContext extends ParserRuleContext {
		public TerminalNode IDENTIFIER() { return getToken(Smalltalk80Parser.IDENTIFIER, 0); }
		public LiteralContext literal() {
			return getRuleContext(LiteralContext.class,0);
		}
		public BlockContext block() {
			return getRuleContext(BlockContext.class,0);
		}
		public TerminalNode LPAREN() { return getToken(Smalltalk80Parser.LPAREN, 0); }
		public ExpressionContext expression() {
			return getRuleContext(ExpressionContext.class,0);
		}
		public TerminalNode RPAREN() { return getToken(Smalltalk80Parser.RPAREN, 0); }
		public PrimaryContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_primary; }
	}

	public final PrimaryContext primary() throws RecognitionException {
		PrimaryContext _localctx = new PrimaryContext(_ctx, getState());
		enterRule(_localctx, 20, RULE_primary);
		try {
			setState(124);
			_errHandler.sync(this);
			switch (_input.LA(1)) {
			case IDENTIFIER:
				enterOuterAlt(_localctx, 1);
				{
				setState(112);
				match(IDENTIFIER);
				}
				break;
			case T__5:
			case NUMBER:
			case STRING:
			case SYMBOL:
			case CHARACTER_CONST:
				enterOuterAlt(_localctx, 2);
				{
				setState(113);
				literal();
				}
				break;
			case LBRACK:
				enterOuterAlt(_localctx, 3);
				{
				setState(114);
				block();
				}
				break;
			case LPAREN:
				enterOuterAlt(_localctx, 4);
				{
				setState(115);
				match(LPAREN);
				setState(116);
				expression();
				setState(117);
				match(RPAREN);
				}
				break;
			case T__0:
				enterOuterAlt(_localctx, 5);
				{
				setState(119);
				match(T__0);
				}
				break;
			case T__1:
				enterOuterAlt(_localctx, 6);
				{
				setState(120);
				match(T__1);
				}
				break;
			case T__2:
				enterOuterAlt(_localctx, 7);
				{
				setState(121);
				match(T__2);
				}
				break;
			case T__3:
				enterOuterAlt(_localctx, 8);
				{
				setState(122);
				match(T__3);
				}
				break;
			case T__4:
				enterOuterAlt(_localctx, 9);
				{
				setState(123);
				match(T__4);
				}
				break;
			default:
				throw new NoViableAltException(this);
			}
		}
		catch (RecognitionException re) {
			_localctx.exception = re;
			_errHandler.reportError(this, re);
			_errHandler.recover(this, re);
		}
		finally {
			exitRule();
		}
		return _localctx;
	}

	@SuppressWarnings("CheckReturnValue")
	public static class LiteralContext extends ParserRuleContext {
		public TerminalNode NUMBER() { return getToken(Smalltalk80Parser.NUMBER, 0); }
		public TerminalNode STRING() { return getToken(Smalltalk80Parser.STRING, 0); }
		public TerminalNode SYMBOL() { return getToken(Smalltalk80Parser.SYMBOL, 0); }
		public TerminalNode CHARACTER_CONST() { return getToken(Smalltalk80Parser.CHARACTER_CONST, 0); }
		public ArrayLiteralContext arrayLiteral() {
			return getRuleContext(ArrayLiteralContext.class,0);
		}
		public LiteralContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_literal; }
	}

	public final LiteralContext literal() throws RecognitionException {
		LiteralContext _localctx = new LiteralContext(_ctx, getState());
		enterRule(_localctx, 22, RULE_literal);
		try {
			setState(131);
			_errHandler.sync(this);
			switch (_input.LA(1)) {
			case NUMBER:
				enterOuterAlt(_localctx, 1);
				{
				setState(126);
				match(NUMBER);
				}
				break;
			case STRING:
				enterOuterAlt(_localctx, 2);
				{
				setState(127);
				match(STRING);
				}
				break;
			case SYMBOL:
				enterOuterAlt(_localctx, 3);
				{
				setState(128);
				match(SYMBOL);
				}
				break;
			case CHARACTER_CONST:
				enterOuterAlt(_localctx, 4);
				{
				setState(129);
				match(CHARACTER_CONST);
				}
				break;
			case T__5:
				enterOuterAlt(_localctx, 5);
				{
				setState(130);
				arrayLiteral();
				}
				break;
			default:
				throw new NoViableAltException(this);
			}
		}
		catch (RecognitionException re) {
			_localctx.exception = re;
			_errHandler.reportError(this, re);
			_errHandler.recover(this, re);
		}
		finally {
			exitRule();
		}
		return _localctx;
	}

	@SuppressWarnings("CheckReturnValue")
	public static class ArrayLiteralContext extends ParserRuleContext {
		public TerminalNode LPAREN() { return getToken(Smalltalk80Parser.LPAREN, 0); }
		public TerminalNode RPAREN() { return getToken(Smalltalk80Parser.RPAREN, 0); }
		public List<LiteralContext> literal() {
			return getRuleContexts(LiteralContext.class);
		}
		public LiteralContext literal(int i) {
			return getRuleContext(LiteralContext.class,i);
		}
		public ArrayLiteralContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_arrayLiteral; }
	}

	public final ArrayLiteralContext arrayLiteral() throws RecognitionException {
		ArrayLiteralContext _localctx = new ArrayLiteralContext(_ctx, getState());
		enterRule(_localctx, 24, RULE_arrayLiteral);
		int _la;
		try {
			enterOuterAlt(_localctx, 1);
			{
			setState(133);
			match(T__5);
			setState(134);
			match(LPAREN);
			setState(138);
			_errHandler.sync(this);
			_la = _input.LA(1);
			while ((((_la) & ~0x3f) == 0 && ((1L << _la) & 1966144L) != 0)) {
				{
				{
				setState(135);
				literal();
				}
				}
				setState(140);
				_errHandler.sync(this);
				_la = _input.LA(1);
			}
			setState(141);
			match(RPAREN);
			}
		}
		catch (RecognitionException re) {
			_localctx.exception = re;
			_errHandler.reportError(this, re);
			_errHandler.recover(this, re);
		}
		finally {
			exitRule();
		}
		return _localctx;
	}

	@SuppressWarnings("CheckReturnValue")
	public static class BlockContext extends ParserRuleContext {
		public TerminalNode LBRACK() { return getToken(Smalltalk80Parser.LBRACK, 0); }
		public BlockBodyContext blockBody() {
			return getRuleContext(BlockBodyContext.class,0);
		}
		public TerminalNode RBRACK() { return getToken(Smalltalk80Parser.RBRACK, 0); }
		public BlockArgsContext blockArgs() {
			return getRuleContext(BlockArgsContext.class,0);
		}
		public TerminalNode PIPE() { return getToken(Smalltalk80Parser.PIPE, 0); }
		public TemporariesContext temporaries() {
			return getRuleContext(TemporariesContext.class,0);
		}
		public BlockContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_block; }
	}

	public final BlockContext block() throws RecognitionException {
		BlockContext _localctx = new BlockContext(_ctx, getState());
		enterRule(_localctx, 26, RULE_block);
		int _la;
		try {
			enterOuterAlt(_localctx, 1);
			{
			setState(143);
			match(LBRACK);
			setState(147);
			_errHandler.sync(this);
			_la = _input.LA(1);
			if (_la==COLON) {
				{
				setState(144);
				blockArgs();
				setState(145);
				match(PIPE);
				}
			}

			setState(150);
			_errHandler.sync(this);
			_la = _input.LA(1);
			if (_la==PIPE) {
				{
				setState(149);
				temporaries();
				}
			}

			setState(152);
			blockBody();
			setState(153);
			match(RBRACK);
			}
		}
		catch (RecognitionException re) {
			_localctx.exception = re;
			_errHandler.reportError(this, re);
			_errHandler.recover(this, re);
		}
		finally {
			exitRule();
		}
		return _localctx;
	}

	@SuppressWarnings("CheckReturnValue")
	public static class BlockArgsContext extends ParserRuleContext {
		public List<TerminalNode> COLON() { return getTokens(Smalltalk80Parser.COLON); }
		public TerminalNode COLON(int i) {
			return getToken(Smalltalk80Parser.COLON, i);
		}
		public List<TerminalNode> IDENTIFIER() { return getTokens(Smalltalk80Parser.IDENTIFIER); }
		public TerminalNode IDENTIFIER(int i) {
			return getToken(Smalltalk80Parser.IDENTIFIER, i);
		}
		public BlockArgsContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_blockArgs; }
	}

	public final BlockArgsContext blockArgs() throws RecognitionException {
		BlockArgsContext _localctx = new BlockArgsContext(_ctx, getState());
		enterRule(_localctx, 28, RULE_blockArgs);
		int _la;
		try {
			enterOuterAlt(_localctx, 1);
			{
			setState(157); 
			_errHandler.sync(this);
			_la = _input.LA(1);
			do {
				{
				{
				setState(155);
				match(COLON);
				setState(156);
				match(IDENTIFIER);
				}
				}
				setState(159); 
				_errHandler.sync(this);
				_la = _input.LA(1);
			} while ( _la==COLON );
			}
		}
		catch (RecognitionException re) {
			_localctx.exception = re;
			_errHandler.reportError(this, re);
			_errHandler.recover(this, re);
		}
		finally {
			exitRule();
		}
		return _localctx;
	}

	@SuppressWarnings("CheckReturnValue")
	public static class BlockBodyContext extends ParserRuleContext {
		public List<ExpressionListContext> expressionList() {
			return getRuleContexts(ExpressionListContext.class);
		}
		public ExpressionListContext expressionList(int i) {
			return getRuleContext(ExpressionListContext.class,i);
		}
		public List<TerminalNode> DOT() { return getTokens(Smalltalk80Parser.DOT); }
		public TerminalNode DOT(int i) {
			return getToken(Smalltalk80Parser.DOT, i);
		}
		public ReturnExprContext returnExpr() {
			return getRuleContext(ReturnExprContext.class,0);
		}
		public ExpressionContext expression() {
			return getRuleContext(ExpressionContext.class,0);
		}
		public BlockBodyContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_blockBody; }
	}

	public final BlockBodyContext blockBody() throws RecognitionException {
		BlockBodyContext _localctx = new BlockBodyContext(_ctx, getState());
		enterRule(_localctx, 30, RULE_blockBody);
		try {
			int _alt;
			enterOuterAlt(_localctx, 1);
			{
			setState(166);
			_errHandler.sync(this);
			_alt = getInterpreter().adaptivePredict(_input,14,_ctx);
			while ( _alt!=2 && _alt!=org.antlr.v4.runtime.atn.ATN.INVALID_ALT_NUMBER ) {
				if ( _alt==1 ) {
					{
					{
					setState(161);
					expressionList();
					setState(162);
					match(DOT);
					}
					} 
				}
				setState(168);
				_errHandler.sync(this);
				_alt = getInterpreter().adaptivePredict(_input,14,_ctx);
			}
			setState(171);
			_errHandler.sync(this);
			switch ( getInterpreter().adaptivePredict(_input,15,_ctx) ) {
			case 1:
				{
				setState(169);
				returnExpr();
				}
				break;
			case 2:
				{
				setState(170);
				expression();
				}
				break;
			}
			}
		}
		catch (RecognitionException re) {
			_localctx.exception = re;
			_errHandler.reportError(this, re);
			_errHandler.recover(this, re);
		}
		finally {
			exitRule();
		}
		return _localctx;
	}

	@SuppressWarnings("CheckReturnValue")
	public static class MethodDefinitionContext extends ParserRuleContext {
		public MethodHeaderContext methodHeader() {
			return getRuleContext(MethodHeaderContext.class,0);
		}
		public MethodBodyContext methodBody() {
			return getRuleContext(MethodBodyContext.class,0);
		}
		public MethodDefinitionContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_methodDefinition; }
	}

	public final MethodDefinitionContext methodDefinition() throws RecognitionException {
		MethodDefinitionContext _localctx = new MethodDefinitionContext(_ctx, getState());
		enterRule(_localctx, 32, RULE_methodDefinition);
		try {
			enterOuterAlt(_localctx, 1);
			{
			setState(173);
			methodHeader();
			setState(174);
			methodBody();
			}
		}
		catch (RecognitionException re) {
			_localctx.exception = re;
			_errHandler.reportError(this, re);
			_errHandler.recover(this, re);
		}
		finally {
			exitRule();
		}
		return _localctx;
	}

	@SuppressWarnings("CheckReturnValue")
	public static class MethodHeaderContext extends ParserRuleContext {
		public UnarySelectorContext unarySelector() {
			return getRuleContext(UnarySelectorContext.class,0);
		}
		public BinarySelectorContext binarySelector() {
			return getRuleContext(BinarySelectorContext.class,0);
		}
		public List<TerminalNode> IDENTIFIER() { return getTokens(Smalltalk80Parser.IDENTIFIER); }
		public TerminalNode IDENTIFIER(int i) {
			return getToken(Smalltalk80Parser.IDENTIFIER, i);
		}
		public List<TerminalNode> KEYWORD() { return getTokens(Smalltalk80Parser.KEYWORD); }
		public TerminalNode KEYWORD(int i) {
			return getToken(Smalltalk80Parser.KEYWORD, i);
		}
		public MethodHeaderContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_methodHeader; }
	}

	public final MethodHeaderContext methodHeader() throws RecognitionException {
		MethodHeaderContext _localctx = new MethodHeaderContext(_ctx, getState());
		enterRule(_localctx, 34, RULE_methodHeader);
		int _la;
		try {
			setState(186);
			_errHandler.sync(this);
			switch (_input.LA(1)) {
			case IDENTIFIER:
				enterOuterAlt(_localctx, 1);
				{
				setState(176);
				unarySelector();
				}
				break;
			case BINARY_SELECTOR:
				enterOuterAlt(_localctx, 2);
				{
				setState(177);
				binarySelector();
				setState(178);
				match(IDENTIFIER);
				}
				break;
			case KEYWORD:
				enterOuterAlt(_localctx, 3);
				{
				setState(182); 
				_errHandler.sync(this);
				_la = _input.LA(1);
				do {
					{
					{
					setState(180);
					match(KEYWORD);
					setState(181);
					match(IDENTIFIER);
					}
					}
					setState(184); 
					_errHandler.sync(this);
					_la = _input.LA(1);
				} while ( _la==KEYWORD );
				}
				break;
			default:
				throw new NoViableAltException(this);
			}
		}
		catch (RecognitionException re) {
			_localctx.exception = re;
			_errHandler.reportError(this, re);
			_errHandler.recover(this, re);
		}
		finally {
			exitRule();
		}
		return _localctx;
	}

	@SuppressWarnings("CheckReturnValue")
	public static class MethodBodyContext extends ParserRuleContext {
		public StatementsContext statements() {
			return getRuleContext(StatementsContext.class,0);
		}
		public PrimitiveDeclarationContext primitiveDeclaration() {
			return getRuleContext(PrimitiveDeclarationContext.class,0);
		}
		public TemporariesContext temporaries() {
			return getRuleContext(TemporariesContext.class,0);
		}
		public MethodBodyContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_methodBody; }
	}

	public final MethodBodyContext methodBody() throws RecognitionException {
		MethodBodyContext _localctx = new MethodBodyContext(_ctx, getState());
		enterRule(_localctx, 36, RULE_methodBody);
		int _la;
		try {
			enterOuterAlt(_localctx, 1);
			{
			setState(189);
			_errHandler.sync(this);
			_la = _input.LA(1);
			if (_la==T__6) {
				{
				setState(188);
				primitiveDeclaration();
				}
			}

			setState(192);
			_errHandler.sync(this);
			_la = _input.LA(1);
			if (_la==PIPE) {
				{
				setState(191);
				temporaries();
				}
			}

			setState(194);
			statements();
			}
		}
		catch (RecognitionException re) {
			_localctx.exception = re;
			_errHandler.reportError(this, re);
			_errHandler.recover(this, re);
		}
		finally {
			exitRule();
		}
		return _localctx;
	}

	@SuppressWarnings("CheckReturnValue")
	public static class PrimitiveDeclarationContext extends ParserRuleContext {
		public TerminalNode NUMBER() { return getToken(Smalltalk80Parser.NUMBER, 0); }
		public PrimitiveDeclarationContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_primitiveDeclaration; }
	}

	public final PrimitiveDeclarationContext primitiveDeclaration() throws RecognitionException {
		PrimitiveDeclarationContext _localctx = new PrimitiveDeclarationContext(_ctx, getState());
		enterRule(_localctx, 38, RULE_primitiveDeclaration);
		try {
			enterOuterAlt(_localctx, 1);
			{
			setState(196);
			match(T__6);
			setState(197);
			match(NUMBER);
			setState(198);
			match(T__7);
			}
		}
		catch (RecognitionException re) {
			_localctx.exception = re;
			_errHandler.reportError(this, re);
			_errHandler.recover(this, re);
		}
		finally {
			exitRule();
		}
		return _localctx;
	}

	@SuppressWarnings("CheckReturnValue")
	public static class TemporariesContext extends ParserRuleContext {
		public List<TerminalNode> PIPE() { return getTokens(Smalltalk80Parser.PIPE); }
		public TerminalNode PIPE(int i) {
			return getToken(Smalltalk80Parser.PIPE, i);
		}
		public List<TerminalNode> IDENTIFIER() { return getTokens(Smalltalk80Parser.IDENTIFIER); }
		public TerminalNode IDENTIFIER(int i) {
			return getToken(Smalltalk80Parser.IDENTIFIER, i);
		}
		public TemporariesContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_temporaries; }
	}

	public final TemporariesContext temporaries() throws RecognitionException {
		TemporariesContext _localctx = new TemporariesContext(_ctx, getState());
		enterRule(_localctx, 40, RULE_temporaries);
		int _la;
		try {
			enterOuterAlt(_localctx, 1);
			{
			setState(200);
			match(PIPE);
			setState(202); 
			_errHandler.sync(this);
			_la = _input.LA(1);
			do {
				{
				{
				setState(201);
				match(IDENTIFIER);
				}
				}
				setState(204); 
				_errHandler.sync(this);
				_la = _input.LA(1);
			} while ( _la==IDENTIFIER );
			setState(206);
			match(PIPE);
			}
		}
		catch (RecognitionException re) {
			_localctx.exception = re;
			_errHandler.reportError(this, re);
			_errHandler.recover(this, re);
		}
		finally {
			exitRule();
		}
		return _localctx;
	}

	@SuppressWarnings("CheckReturnValue")
	public static class StatementsContext extends ParserRuleContext {
		public ExpressionListContext expressionList() {
			return getRuleContext(ExpressionListContext.class,0);
		}
		public StatementsContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_statements; }
	}

	public final StatementsContext statements() throws RecognitionException {
		StatementsContext _localctx = new StatementsContext(_ctx, getState());
		enterRule(_localctx, 42, RULE_statements);
		try {
			enterOuterAlt(_localctx, 1);
			{
			setState(208);
			expressionList();
			}
		}
		catch (RecognitionException re) {
			_localctx.exception = re;
			_errHandler.reportError(this, re);
			_errHandler.recover(this, re);
		}
		finally {
			exitRule();
		}
		return _localctx;
	}

	@SuppressWarnings("CheckReturnValue")
	public static class ClassDefinitionContext extends ParserRuleContext {
		public TerminalNode IDENTIFIER() { return getToken(Smalltalk80Parser.IDENTIFIER, 0); }
		public List<TerminalNode> STRING() { return getTokens(Smalltalk80Parser.STRING); }
		public TerminalNode STRING(int i) {
			return getToken(Smalltalk80Parser.STRING, i);
		}
		public ClassMethodDictionaryContext classMethodDictionary() {
			return getRuleContext(ClassMethodDictionaryContext.class,0);
		}
		public ClassDefinitionContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_classDefinition; }
	}

	public final ClassDefinitionContext classDefinition() throws RecognitionException {
		ClassDefinitionContext _localctx = new ClassDefinitionContext(_ctx, getState());
		enterRule(_localctx, 44, RULE_classDefinition);
		int _la;
		try {
			enterOuterAlt(_localctx, 1);
			{
			setState(210);
			match(T__8);
			setState(211);
			match(IDENTIFIER);
			setState(212);
			match(T__9);
			setState(213);
			match(STRING);
			setState(214);
			match(T__10);
			setState(215);
			match(STRING);
			setState(216);
			match(T__11);
			setState(217);
			match(STRING);
			setState(218);
			match(T__12);
			setState(219);
			match(STRING);
			setState(220);
			match(T__13);
			setState(221);
			match(STRING);
			setState(223);
			_errHandler.sync(this);
			_la = _input.LA(1);
			if (_la==T__14) {
				{
				setState(222);
				classMethodDictionary();
				}
			}

			}
		}
		catch (RecognitionException re) {
			_localctx.exception = re;
			_errHandler.reportError(this, re);
			_errHandler.recover(this, re);
		}
		finally {
			exitRule();
		}
		return _localctx;
	}

	@SuppressWarnings("CheckReturnValue")
	public static class ClassMethodDictionaryContext extends ParserRuleContext {
		public TerminalNode STRING() { return getToken(Smalltalk80Parser.STRING, 0); }
		public MethodListContext methodList() {
			return getRuleContext(MethodListContext.class,0);
		}
		public ClassMethodDictionaryContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_classMethodDictionary; }
	}

	public final ClassMethodDictionaryContext classMethodDictionary() throws RecognitionException {
		ClassMethodDictionaryContext _localctx = new ClassMethodDictionaryContext(_ctx, getState());
		enterRule(_localctx, 46, RULE_classMethodDictionary);
		try {
			enterOuterAlt(_localctx, 1);
			{
			setState(225);
			match(T__14);
			setState(226);
			match(STRING);
			setState(227);
			methodList();
			}
		}
		catch (RecognitionException re) {
			_localctx.exception = re;
			_errHandler.reportError(this, re);
			_errHandler.recover(this, re);
		}
		finally {
			exitRule();
		}
		return _localctx;
	}

	@SuppressWarnings("CheckReturnValue")
	public static class MethodListContext extends ParserRuleContext {
		public List<MethodDefinitionContext> methodDefinition() {
			return getRuleContexts(MethodDefinitionContext.class);
		}
		public MethodDefinitionContext methodDefinition(int i) {
			return getRuleContext(MethodDefinitionContext.class,i);
		}
		public List<TerminalNode> DOT() { return getTokens(Smalltalk80Parser.DOT); }
		public TerminalNode DOT(int i) {
			return getToken(Smalltalk80Parser.DOT, i);
		}
		public MethodListContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_methodList; }
	}

	public final MethodListContext methodList() throws RecognitionException {
		MethodListContext _localctx = new MethodListContext(_ctx, getState());
		enterRule(_localctx, 48, RULE_methodList);
		try {
			int _alt;
			enterOuterAlt(_localctx, 1);
			{
			setState(234);
			_errHandler.sync(this);
			_alt = getInterpreter().adaptivePredict(_input,22,_ctx);
			while ( _alt!=2 && _alt!=org.antlr.v4.runtime.atn.ATN.INVALID_ALT_NUMBER ) {
				if ( _alt==1 ) {
					{
					{
					setState(229);
					methodDefinition();
					setState(230);
					match(DOT);
					}
					} 
				}
				setState(236);
				_errHandler.sync(this);
				_alt = getInterpreter().adaptivePredict(_input,22,_ctx);
			}
			}
		}
		catch (RecognitionException re) {
			_localctx.exception = re;
			_errHandler.reportError(this, re);
			_errHandler.recover(this, re);
		}
		finally {
			exitRule();
		}
		return _localctx;
	}

	@SuppressWarnings("CheckReturnValue")
	public static class UnarySelectorContext extends ParserRuleContext {
		public TerminalNode IDENTIFIER() { return getToken(Smalltalk80Parser.IDENTIFIER, 0); }
		public UnarySelectorContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_unarySelector; }
	}

	public final UnarySelectorContext unarySelector() throws RecognitionException {
		UnarySelectorContext _localctx = new UnarySelectorContext(_ctx, getState());
		enterRule(_localctx, 50, RULE_unarySelector);
		try {
			enterOuterAlt(_localctx, 1);
			{
			setState(237);
			match(IDENTIFIER);
			}
		}
		catch (RecognitionException re) {
			_localctx.exception = re;
			_errHandler.reportError(this, re);
			_errHandler.recover(this, re);
		}
		finally {
			exitRule();
		}
		return _localctx;
	}

	@SuppressWarnings("CheckReturnValue")
	public static class BinarySelectorContext extends ParserRuleContext {
		public TerminalNode BINARY_SELECTOR() { return getToken(Smalltalk80Parser.BINARY_SELECTOR, 0); }
		public BinarySelectorContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_binarySelector; }
	}

	public final BinarySelectorContext binarySelector() throws RecognitionException {
		BinarySelectorContext _localctx = new BinarySelectorContext(_ctx, getState());
		enterRule(_localctx, 52, RULE_binarySelector);
		try {
			enterOuterAlt(_localctx, 1);
			{
			setState(239);
			match(BINARY_SELECTOR);
			}
		}
		catch (RecognitionException re) {
			_localctx.exception = re;
			_errHandler.reportError(this, re);
			_errHandler.recover(this, re);
		}
		finally {
			exitRule();
		}
		return _localctx;
	}

	public static final String _serializedATN =
		"\u0004\u0001!\u00f2\u0002\u0000\u0007\u0000\u0002\u0001\u0007\u0001\u0002"+
		"\u0002\u0007\u0002\u0002\u0003\u0007\u0003\u0002\u0004\u0007\u0004\u0002"+
		"\u0005\u0007\u0005\u0002\u0006\u0007\u0006\u0002\u0007\u0007\u0007\u0002"+
		"\b\u0007\b\u0002\t\u0007\t\u0002\n\u0007\n\u0002\u000b\u0007\u000b\u0002"+
		"\f\u0007\f\u0002\r\u0007\r\u0002\u000e\u0007\u000e\u0002\u000f\u0007\u000f"+
		"\u0002\u0010\u0007\u0010\u0002\u0011\u0007\u0011\u0002\u0012\u0007\u0012"+
		"\u0002\u0013\u0007\u0013\u0002\u0014\u0007\u0014\u0002\u0015\u0007\u0015"+
		"\u0002\u0016\u0007\u0016\u0002\u0017\u0007\u0017\u0002\u0018\u0007\u0018"+
		"\u0002\u0019\u0007\u0019\u0002\u001a\u0007\u001a\u0001\u0000\u0001\u0000"+
		"\u0001\u0000\u0005\u0000:\b\u0000\n\u0000\f\u0000=\t\u0000\u0001\u0000"+
		"\u0001\u0000\u0001\u0001\u0001\u0001\u0001\u0001\u0005\u0001D\b\u0001"+
		"\n\u0001\f\u0001G\t\u0001\u0001\u0002\u0001\u0002\u0001\u0002\u0003\u0002"+
		"L\b\u0002\u0001\u0003\u0001\u0003\u0001\u0003\u0001\u0003\u0001\u0004"+
		"\u0001\u0004\u0001\u0004\u0001\u0005\u0001\u0005\u0001\u0005\u0005\u0005"+
		"X\b\u0005\n\u0005\f\u0005[\t\u0005\u0001\u0006\u0001\u0006\u0001\u0006"+
		"\u0001\u0006\u0005\u0006a\b\u0006\n\u0006\f\u0006d\t\u0006\u0001\u0007"+
		"\u0001\u0007\u0001\b\u0001\b\u0001\b\u0001\t\u0001\t\u0004\tm\b\t\u000b"+
		"\t\f\tn\u0001\n\u0001\n\u0001\n\u0001\n\u0001\n\u0001\n\u0001\n\u0001"+
		"\n\u0001\n\u0001\n\u0001\n\u0001\n\u0003\n}\b\n\u0001\u000b\u0001\u000b"+
		"\u0001\u000b\u0001\u000b\u0001\u000b\u0003\u000b\u0084\b\u000b\u0001\f"+
		"\u0001\f\u0001\f\u0005\f\u0089\b\f\n\f\f\f\u008c\t\f\u0001\f\u0001\f\u0001"+
		"\r\u0001\r\u0001\r\u0001\r\u0003\r\u0094\b\r\u0001\r\u0003\r\u0097\b\r"+
		"\u0001\r\u0001\r\u0001\r\u0001\u000e\u0001\u000e\u0004\u000e\u009e\b\u000e"+
		"\u000b\u000e\f\u000e\u009f\u0001\u000f\u0001\u000f\u0001\u000f\u0005\u000f"+
		"\u00a5\b\u000f\n\u000f\f\u000f\u00a8\t\u000f\u0001\u000f\u0001\u000f\u0003"+
		"\u000f\u00ac\b\u000f\u0001\u0010\u0001\u0010\u0001\u0010\u0001\u0011\u0001"+
		"\u0011\u0001\u0011\u0001\u0011\u0001\u0011\u0001\u0011\u0004\u0011\u00b7"+
		"\b\u0011\u000b\u0011\f\u0011\u00b8\u0003\u0011\u00bb\b\u0011\u0001\u0012"+
		"\u0003\u0012\u00be\b\u0012\u0001\u0012\u0003\u0012\u00c1\b\u0012\u0001"+
		"\u0012\u0001\u0012\u0001\u0013\u0001\u0013\u0001\u0013\u0001\u0013\u0001"+
		"\u0014\u0001\u0014\u0004\u0014\u00cb\b\u0014\u000b\u0014\f\u0014\u00cc"+
		"\u0001\u0014\u0001\u0014\u0001\u0015\u0001\u0015\u0001\u0016\u0001\u0016"+
		"\u0001\u0016\u0001\u0016\u0001\u0016\u0001\u0016\u0001\u0016\u0001\u0016"+
		"\u0001\u0016\u0001\u0016\u0001\u0016\u0001\u0016\u0001\u0016\u0003\u0016"+
		"\u00e0\b\u0016\u0001\u0017\u0001\u0017\u0001\u0017\u0001\u0017\u0001\u0018"+
		"\u0001\u0018\u0001\u0018\u0005\u0018\u00e9\b\u0018\n\u0018\f\u0018\u00ec"+
		"\t\u0018\u0001\u0019\u0001\u0019\u0001\u001a\u0001\u001a\u0001\u001a\u0000"+
		"\u0000\u001b\u0000\u0002\u0004\u0006\b\n\f\u000e\u0010\u0012\u0014\u0016"+
		"\u0018\u001a\u001c\u001e \"$&(*,.024\u0000\u0000\u00fc\u0000;\u0001\u0000"+
		"\u0000\u0000\u0002@\u0001\u0000\u0000\u0000\u0004K\u0001\u0000\u0000\u0000"+
		"\u0006M\u0001\u0000\u0000\u0000\bQ\u0001\u0000\u0000\u0000\nT\u0001\u0000"+
		"\u0000\u0000\f\\\u0001\u0000\u0000\u0000\u000ee\u0001\u0000\u0000\u0000"+
		"\u0010g\u0001\u0000\u0000\u0000\u0012l\u0001\u0000\u0000\u0000\u0014|"+
		"\u0001\u0000\u0000\u0000\u0016\u0083\u0001\u0000\u0000\u0000\u0018\u0085"+
		"\u0001\u0000\u0000\u0000\u001a\u008f\u0001\u0000\u0000\u0000\u001c\u009d"+
		"\u0001\u0000\u0000\u0000\u001e\u00a6\u0001\u0000\u0000\u0000 \u00ad\u0001"+
		"\u0000\u0000\u0000\"\u00ba\u0001\u0000\u0000\u0000$\u00bd\u0001\u0000"+
		"\u0000\u0000&\u00c4\u0001\u0000\u0000\u0000(\u00c8\u0001\u0000\u0000\u0000"+
		"*\u00d0\u0001\u0000\u0000\u0000,\u00d2\u0001\u0000\u0000\u0000.\u00e1"+
		"\u0001\u0000\u0000\u00000\u00ea\u0001\u0000\u0000\u00002\u00ed\u0001\u0000"+
		"\u0000\u00004\u00ef\u0001\u0000\u0000\u00006:\u0003,\u0016\u00007:\u0003"+
		" \u0010\u00008:\u0003\u0002\u0001\u000096\u0001\u0000\u0000\u000097\u0001"+
		"\u0000\u0000\u000098\u0001\u0000\u0000\u0000:=\u0001\u0000\u0000\u0000"+
		";9\u0001\u0000\u0000\u0000;<\u0001\u0000\u0000\u0000<>\u0001\u0000\u0000"+
		"\u0000=;\u0001\u0000\u0000\u0000>?\u0005\u0000\u0000\u0001?\u0001\u0001"+
		"\u0000\u0000\u0000@E\u0003\u0004\u0002\u0000AB\u0005\u001f\u0000\u0000"+
		"BD\u0003\u0004\u0002\u0000CA\u0001\u0000\u0000\u0000DG\u0001\u0000\u0000"+
		"\u0000EC\u0001\u0000\u0000\u0000EF\u0001\u0000\u0000\u0000F\u0003\u0001"+
		"\u0000\u0000\u0000GE\u0001\u0000\u0000\u0000HL\u0003\u0006\u0003\u0000"+
		"IL\u0003\b\u0004\u0000JL\u0003\n\u0005\u0000KH\u0001\u0000\u0000\u0000"+
		"KI\u0001\u0000\u0000\u0000KJ\u0001\u0000\u0000\u0000L\u0005\u0001\u0000"+
		"\u0000\u0000MN\u0005\u0010\u0000\u0000NO\u0005\u001e\u0000\u0000OP\u0003"+
		"\u0004\u0002\u0000P\u0007\u0001\u0000\u0000\u0000QR\u0005!\u0000\u0000"+
		"RS\u0003\u0004\u0002\u0000S\t\u0001\u0000\u0000\u0000TY\u0003\f\u0006"+
		"\u0000UV\u0005 \u0000\u0000VX\u0003\f\u0006\u0000WU\u0001\u0000\u0000"+
		"\u0000X[\u0001\u0000\u0000\u0000YW\u0001\u0000\u0000\u0000YZ\u0001\u0000"+
		"\u0000\u0000Z\u000b\u0001\u0000\u0000\u0000[Y\u0001\u0000\u0000\u0000"+
		"\\b\u0003\u0014\n\u0000]a\u0003\u000e\u0007\u0000^a\u0003\u0010\b\u0000"+
		"_a\u0003\u0012\t\u0000`]\u0001\u0000\u0000\u0000`^\u0001\u0000\u0000\u0000"+
		"`_\u0001\u0000\u0000\u0000ad\u0001\u0000\u0000\u0000b`\u0001\u0000\u0000"+
		"\u0000bc\u0001\u0000\u0000\u0000c\r\u0001\u0000\u0000\u0000db\u0001\u0000"+
		"\u0000\u0000ef\u0005\u0010\u0000\u0000f\u000f\u0001\u0000\u0000\u0000"+
		"gh\u0005\u0016\u0000\u0000hi\u0003\u0014\n\u0000i\u0011\u0001\u0000\u0000"+
		"\u0000jk\u0005\u0017\u0000\u0000km\u0003\u0014\n\u0000lj\u0001\u0000\u0000"+
		"\u0000mn\u0001\u0000\u0000\u0000nl\u0001\u0000\u0000\u0000no\u0001\u0000"+
		"\u0000\u0000o\u0013\u0001\u0000\u0000\u0000p}\u0005\u0010\u0000\u0000"+
		"q}\u0003\u0016\u000b\u0000r}\u0003\u001a\r\u0000st\u0005\u0018\u0000\u0000"+
		"tu\u0003\u0004\u0002\u0000uv\u0005\u0019\u0000\u0000v}\u0001\u0000\u0000"+
		"\u0000w}\u0005\u0001\u0000\u0000x}\u0005\u0002\u0000\u0000y}\u0005\u0003"+
		"\u0000\u0000z}\u0005\u0004\u0000\u0000{}\u0005\u0005\u0000\u0000|p\u0001"+
		"\u0000\u0000\u0000|q\u0001\u0000\u0000\u0000|r\u0001\u0000\u0000\u0000"+
		"|s\u0001\u0000\u0000\u0000|w\u0001\u0000\u0000\u0000|x\u0001\u0000\u0000"+
		"\u0000|y\u0001\u0000\u0000\u0000|z\u0001\u0000\u0000\u0000|{\u0001\u0000"+
		"\u0000\u0000}\u0015\u0001\u0000\u0000\u0000~\u0084\u0005\u0011\u0000\u0000"+
		"\u007f\u0084\u0005\u0012\u0000\u0000\u0080\u0084\u0005\u0013\u0000\u0000"+
		"\u0081\u0084\u0005\u0014\u0000\u0000\u0082\u0084\u0003\u0018\f\u0000\u0083"+
		"~\u0001\u0000\u0000\u0000\u0083\u007f\u0001\u0000\u0000\u0000\u0083\u0080"+
		"\u0001\u0000\u0000\u0000\u0083\u0081\u0001\u0000\u0000\u0000\u0083\u0082"+
		"\u0001\u0000\u0000\u0000\u0084\u0017\u0001\u0000\u0000\u0000\u0085\u0086"+
		"\u0005\u0006\u0000\u0000\u0086\u008a\u0005\u0018\u0000\u0000\u0087\u0089"+
		"\u0003\u0016\u000b\u0000\u0088\u0087\u0001\u0000\u0000\u0000\u0089\u008c"+
		"\u0001\u0000\u0000\u0000\u008a\u0088\u0001\u0000\u0000\u0000\u008a\u008b"+
		"\u0001\u0000\u0000\u0000\u008b\u008d\u0001\u0000\u0000\u0000\u008c\u008a"+
		"\u0001\u0000\u0000\u0000\u008d\u008e\u0005\u0019\u0000\u0000\u008e\u0019"+
		"\u0001\u0000\u0000\u0000\u008f\u0093\u0005\u001a\u0000\u0000\u0090\u0091"+
		"\u0003\u001c\u000e\u0000\u0091\u0092\u0005\u001c\u0000\u0000\u0092\u0094"+
		"\u0001\u0000\u0000\u0000\u0093\u0090\u0001\u0000\u0000\u0000\u0093\u0094"+
		"\u0001\u0000\u0000\u0000\u0094\u0096\u0001\u0000\u0000\u0000\u0095\u0097"+
		"\u0003(\u0014\u0000\u0096\u0095\u0001\u0000\u0000\u0000\u0096\u0097\u0001"+
		"\u0000\u0000\u0000\u0097\u0098\u0001\u0000\u0000\u0000\u0098\u0099\u0003"+
		"\u001e\u000f\u0000\u0099\u009a\u0005\u001b\u0000\u0000\u009a\u001b\u0001"+
		"\u0000\u0000\u0000\u009b\u009c\u0005\u001d\u0000\u0000\u009c\u009e\u0005"+
		"\u0010\u0000\u0000\u009d\u009b\u0001\u0000\u0000\u0000\u009e\u009f\u0001"+
		"\u0000\u0000\u0000\u009f\u009d\u0001\u0000\u0000\u0000\u009f\u00a0\u0001"+
		"\u0000\u0000\u0000\u00a0\u001d\u0001\u0000\u0000\u0000\u00a1\u00a2\u0003"+
		"\u0002\u0001\u0000\u00a2\u00a3\u0005\u001f\u0000\u0000\u00a3\u00a5\u0001"+
		"\u0000\u0000\u0000\u00a4\u00a1\u0001\u0000\u0000\u0000\u00a5\u00a8\u0001"+
		"\u0000\u0000\u0000\u00a6\u00a4\u0001\u0000\u0000\u0000\u00a6\u00a7\u0001"+
		"\u0000\u0000\u0000\u00a7\u00ab\u0001\u0000\u0000\u0000\u00a8\u00a6\u0001"+
		"\u0000\u0000\u0000\u00a9\u00ac\u0003\b\u0004\u0000\u00aa\u00ac\u0003\u0004"+
		"\u0002\u0000\u00ab\u00a9\u0001\u0000\u0000\u0000\u00ab\u00aa\u0001\u0000"+
		"\u0000\u0000\u00ab\u00ac\u0001\u0000\u0000\u0000\u00ac\u001f\u0001\u0000"+
		"\u0000\u0000\u00ad\u00ae\u0003\"\u0011\u0000\u00ae\u00af\u0003$\u0012"+
		"\u0000\u00af!\u0001\u0000\u0000\u0000\u00b0\u00bb\u00032\u0019\u0000\u00b1"+
		"\u00b2\u00034\u001a\u0000\u00b2\u00b3\u0005\u0010\u0000\u0000\u00b3\u00bb"+
		"\u0001\u0000\u0000\u0000\u00b4\u00b5\u0005\u0017\u0000\u0000\u00b5\u00b7"+
		"\u0005\u0010\u0000\u0000\u00b6\u00b4\u0001\u0000\u0000\u0000\u00b7\u00b8"+
		"\u0001\u0000\u0000\u0000\u00b8\u00b6\u0001\u0000\u0000\u0000\u00b8\u00b9"+
		"\u0001\u0000\u0000\u0000\u00b9\u00bb\u0001\u0000\u0000\u0000\u00ba\u00b0"+
		"\u0001\u0000\u0000\u0000\u00ba\u00b1\u0001\u0000\u0000\u0000\u00ba\u00b6"+
		"\u0001\u0000\u0000\u0000\u00bb#\u0001\u0000\u0000\u0000\u00bc\u00be\u0003"+
		"&\u0013\u0000\u00bd\u00bc\u0001\u0000\u0000\u0000\u00bd\u00be\u0001\u0000"+
		"\u0000\u0000\u00be\u00c0\u0001\u0000\u0000\u0000\u00bf\u00c1\u0003(\u0014"+
		"\u0000\u00c0\u00bf\u0001\u0000\u0000\u0000\u00c0\u00c1\u0001\u0000\u0000"+
		"\u0000\u00c1\u00c2\u0001\u0000\u0000\u0000\u00c2\u00c3\u0003*\u0015\u0000"+
		"\u00c3%\u0001\u0000\u0000\u0000\u00c4\u00c5\u0005\u0007\u0000\u0000\u00c5"+
		"\u00c6\u0005\u0011\u0000\u0000\u00c6\u00c7\u0005\b\u0000\u0000\u00c7\'"+
		"\u0001\u0000\u0000\u0000\u00c8\u00ca\u0005\u001c\u0000\u0000\u00c9\u00cb"+
		"\u0005\u0010\u0000\u0000\u00ca\u00c9\u0001\u0000\u0000\u0000\u00cb\u00cc"+
		"\u0001\u0000\u0000\u0000\u00cc\u00ca\u0001\u0000\u0000\u0000\u00cc\u00cd"+
		"\u0001\u0000\u0000\u0000\u00cd\u00ce\u0001\u0000\u0000\u0000\u00ce\u00cf"+
		"\u0005\u001c\u0000\u0000\u00cf)\u0001\u0000\u0000\u0000\u00d0\u00d1\u0003"+
		"\u0002\u0001\u0000\u00d1+\u0001\u0000\u0000\u0000\u00d2\u00d3\u0005\t"+
		"\u0000\u0000\u00d3\u00d4\u0005\u0010\u0000\u0000\u00d4\u00d5\u0005\n\u0000"+
		"\u0000\u00d5\u00d6\u0005\u0012\u0000\u0000\u00d6\u00d7\u0005\u000b\u0000"+
		"\u0000\u00d7\u00d8\u0005\u0012\u0000\u0000\u00d8\u00d9\u0005\f\u0000\u0000"+
		"\u00d9\u00da\u0005\u0012\u0000\u0000\u00da\u00db\u0005\r\u0000\u0000\u00db"+
		"\u00dc\u0005\u0012\u0000\u0000\u00dc\u00dd\u0005\u000e\u0000\u0000\u00dd"+
		"\u00df\u0005\u0012\u0000\u0000\u00de\u00e0\u0003.\u0017\u0000\u00df\u00de"+
		"\u0001\u0000\u0000\u0000\u00df\u00e0\u0001\u0000\u0000\u0000\u00e0-\u0001"+
		"\u0000\u0000\u0000\u00e1\u00e2\u0005\u000f\u0000\u0000\u00e2\u00e3\u0005"+
		"\u0012\u0000\u0000\u00e3\u00e4\u00030\u0018\u0000\u00e4/\u0001\u0000\u0000"+
		"\u0000\u00e5\u00e6\u0003 \u0010\u0000\u00e6\u00e7\u0005\u001f\u0000\u0000"+
		"\u00e7\u00e9\u0001\u0000\u0000\u0000\u00e8\u00e5\u0001\u0000\u0000\u0000"+
		"\u00e9\u00ec\u0001\u0000\u0000\u0000\u00ea\u00e8\u0001\u0000\u0000\u0000"+
		"\u00ea\u00eb\u0001\u0000\u0000\u0000\u00eb1\u0001\u0000\u0000\u0000\u00ec"+
		"\u00ea\u0001\u0000\u0000\u0000\u00ed\u00ee\u0005\u0010\u0000\u0000\u00ee"+
		"3\u0001\u0000\u0000\u0000\u00ef\u00f0\u0005\u0016\u0000\u0000\u00f05\u0001"+
		"\u0000\u0000\u0000\u00179;EKY`bn|\u0083\u008a\u0093\u0096\u009f\u00a6"+
		"\u00ab\u00b8\u00ba\u00bd\u00c0\u00cc\u00df\u00ea";
	public static final ATN _ATN =
		new ATNDeserializer().deserialize(_serializedATN.toCharArray());
	static {
		_decisionToDFA = new DFA[_ATN.getNumberOfDecisions()];
		for (int i = 0; i < _ATN.getNumberOfDecisions(); i++) {
			_decisionToDFA[i] = new DFA(_ATN.getDecisionState(i), i);
		}
	}
}