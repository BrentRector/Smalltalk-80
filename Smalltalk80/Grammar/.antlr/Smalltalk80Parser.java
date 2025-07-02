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
		T__9=10, T__10=11, T__11=12, T__12=13, SPECIALCHARACTER=14, CHARACTER=15, 
		ASSIGN_ARROW=16, NUMBER=17, IDENTIFIER=18, STRING=19, COMMENT=20, WS=21;
	public static final int
		RULE_identifier = 0, RULE_string = 1, RULE_symbol = 2, RULE_symbolConstant = 3, 
		RULE_characterConstant = 4, RULE_array = 5, RULE_arrayConstant = 6, RULE_literal = 7, 
		RULE_variableName = 8, RULE_unarySelector = 9, RULE_binarySelector = 10, 
		RULE_keyword = 11, RULE_primary = 12, RULE_unaryObjectDescription = 13, 
		RULE_binaryObjectDescription = 14, RULE_keywordExpression = 15, RULE_messageExpression = 16, 
		RULE_cascadedMessageExpression = 17, RULE_expression = 18, RULE_statements = 19, 
		RULE_statement = 20, RULE_block = 21, RULE_temporaries = 22, RULE_method = 23, 
		RULE_messagePattern = 24;
	private static String[] makeRuleNames() {
		return new String[] {
			"identifier", "string", "symbol", "symbolConstant", "characterConstant", 
			"array", "arrayConstant", "literal", "variableName", "unarySelector", 
			"binarySelector", "keyword", "primary", "unaryObjectDescription", "binaryObjectDescription", 
			"keywordExpression", "messageExpression", "cascadedMessageExpression", 
			"expression", "statements", "statement", "block", "temporaries", "method", 
			"messagePattern"
		};
	}
	public static final String[] ruleNames = makeRuleNames();

	private static String[] makeLiteralNames() {
		return new String[] {
			null, "'#'", "'$'", "'''", "'\"'", "'('", "')'", "'-'", "':'", "'.'", 
			"'!'", "'['", "']'", "'|'"
		};
	}
	private static final String[] _LITERAL_NAMES = makeLiteralNames();
	private static String[] makeSymbolicNames() {
		return new String[] {
			null, null, null, null, null, null, null, null, null, null, null, null, 
			null, null, "SPECIALCHARACTER", "CHARACTER", "ASSIGN_ARROW", "NUMBER", 
			"IDENTIFIER", "STRING", "COMMENT", "WS"
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
	public static class IdentifierContext extends ParserRuleContext {
		public TerminalNode IDENTIFIER() { return getToken(Smalltalk80Parser.IDENTIFIER, 0); }
		public IdentifierContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_identifier; }
	}

	public final IdentifierContext identifier() throws RecognitionException {
		IdentifierContext _localctx = new IdentifierContext(_ctx, getState());
		enterRule(_localctx, 0, RULE_identifier);
		try {
			enterOuterAlt(_localctx, 1);
			{
			setState(50);
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
	public static class StringContext extends ParserRuleContext {
		public TerminalNode STRING() { return getToken(Smalltalk80Parser.STRING, 0); }
		public StringContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_string; }
	}

	public final StringContext string() throws RecognitionException {
		StringContext _localctx = new StringContext(_ctx, getState());
		enterRule(_localctx, 2, RULE_string);
		try {
			enterOuterAlt(_localctx, 1);
			{
			setState(52);
			match(STRING);
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
	public static class SymbolContext extends ParserRuleContext {
		public IdentifierContext identifier() {
			return getRuleContext(IdentifierContext.class,0);
		}
		public BinarySelectorContext binarySelector() {
			return getRuleContext(BinarySelectorContext.class,0);
		}
		public List<KeywordContext> keyword() {
			return getRuleContexts(KeywordContext.class);
		}
		public KeywordContext keyword(int i) {
			return getRuleContext(KeywordContext.class,i);
		}
		public SymbolContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_symbol; }
	}

	public final SymbolContext symbol() throws RecognitionException {
		SymbolContext _localctx = new SymbolContext(_ctx, getState());
		enterRule(_localctx, 4, RULE_symbol);
		try {
			int _alt;
			setState(61);
			_errHandler.sync(this);
			switch ( getInterpreter().adaptivePredict(_input,1,_ctx) ) {
			case 1:
				enterOuterAlt(_localctx, 1);
				{
				setState(54);
				identifier();
				}
				break;
			case 2:
				enterOuterAlt(_localctx, 2);
				{
				setState(55);
				binarySelector();
				}
				break;
			case 3:
				enterOuterAlt(_localctx, 3);
				{
				setState(57); 
				_errHandler.sync(this);
				_alt = 1;
				do {
					switch (_alt) {
					case 1:
						{
						{
						setState(56);
						keyword();
						}
						}
						break;
					default:
						throw new NoViableAltException(this);
					}
					setState(59); 
					_errHandler.sync(this);
					_alt = getInterpreter().adaptivePredict(_input,0,_ctx);
				} while ( _alt!=2 && _alt!=org.antlr.v4.runtime.atn.ATN.INVALID_ALT_NUMBER );
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
	public static class SymbolConstantContext extends ParserRuleContext {
		public SymbolContext symbol() {
			return getRuleContext(SymbolContext.class,0);
		}
		public SymbolConstantContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_symbolConstant; }
	}

	public final SymbolConstantContext symbolConstant() throws RecognitionException {
		SymbolConstantContext _localctx = new SymbolConstantContext(_ctx, getState());
		enterRule(_localctx, 6, RULE_symbolConstant);
		try {
			enterOuterAlt(_localctx, 1);
			{
			setState(63);
			match(T__0);
			setState(64);
			symbol();
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
	public static class CharacterConstantContext extends ParserRuleContext {
		public TerminalNode CHARACTER() { return getToken(Smalltalk80Parser.CHARACTER, 0); }
		public CharacterConstantContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_characterConstant; }
	}

	public final CharacterConstantContext characterConstant() throws RecognitionException {
		CharacterConstantContext _localctx = new CharacterConstantContext(_ctx, getState());
		enterRule(_localctx, 8, RULE_characterConstant);
		int _la;
		try {
			enterOuterAlt(_localctx, 1);
			{
			setState(66);
			match(T__1);
			setState(67);
			_la = _input.LA(1);
			if ( !((((_la) & ~0x3f) == 0 && ((1L << _la) & 32792L) != 0)) ) {
			_errHandler.recoverInline(this);
			}
			else {
				if ( _input.LA(1)==Token.EOF ) matchedEOF = true;
				_errHandler.reportMatch(this);
				consume();
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
	public static class ArrayContext extends ParserRuleContext {
		public List<TerminalNode> NUMBER() { return getTokens(Smalltalk80Parser.NUMBER); }
		public TerminalNode NUMBER(int i) {
			return getToken(Smalltalk80Parser.NUMBER, i);
		}
		public List<SymbolContext> symbol() {
			return getRuleContexts(SymbolContext.class);
		}
		public SymbolContext symbol(int i) {
			return getRuleContext(SymbolContext.class,i);
		}
		public List<StringContext> string() {
			return getRuleContexts(StringContext.class);
		}
		public StringContext string(int i) {
			return getRuleContext(StringContext.class,i);
		}
		public List<CharacterConstantContext> characterConstant() {
			return getRuleContexts(CharacterConstantContext.class);
		}
		public CharacterConstantContext characterConstant(int i) {
			return getRuleContext(CharacterConstantContext.class,i);
		}
		public List<ArrayContext> array() {
			return getRuleContexts(ArrayContext.class);
		}
		public ArrayContext array(int i) {
			return getRuleContext(ArrayContext.class,i);
		}
		public ArrayContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_array; }
	}

	public final ArrayContext array() throws RecognitionException {
		ArrayContext _localctx = new ArrayContext(_ctx, getState());
		enterRule(_localctx, 10, RULE_array);
		int _la;
		try {
			enterOuterAlt(_localctx, 1);
			{
			setState(69);
			match(T__4);
			setState(77);
			_errHandler.sync(this);
			_la = _input.LA(1);
			while ((((_la) & ~0x3f) == 0 && ((1L << _la) & 934052L) != 0)) {
				{
				setState(75);
				_errHandler.sync(this);
				switch (_input.LA(1)) {
				case NUMBER:
					{
					setState(70);
					match(NUMBER);
					}
					break;
				case T__6:
				case SPECIALCHARACTER:
				case IDENTIFIER:
					{
					setState(71);
					symbol();
					}
					break;
				case STRING:
					{
					setState(72);
					string();
					}
					break;
				case T__1:
					{
					setState(73);
					characterConstant();
					}
					break;
				case T__4:
					{
					setState(74);
					array();
					}
					break;
				default:
					throw new NoViableAltException(this);
				}
				}
				setState(79);
				_errHandler.sync(this);
				_la = _input.LA(1);
			}
			setState(80);
			match(T__5);
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
	public static class ArrayConstantContext extends ParserRuleContext {
		public ArrayContext array() {
			return getRuleContext(ArrayContext.class,0);
		}
		public ArrayConstantContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_arrayConstant; }
	}

	public final ArrayConstantContext arrayConstant() throws RecognitionException {
		ArrayConstantContext _localctx = new ArrayConstantContext(_ctx, getState());
		enterRule(_localctx, 12, RULE_arrayConstant);
		try {
			enterOuterAlt(_localctx, 1);
			{
			setState(82);
			match(T__0);
			setState(83);
			array();
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
		public SymbolConstantContext symbolConstant() {
			return getRuleContext(SymbolConstantContext.class,0);
		}
		public CharacterConstantContext characterConstant() {
			return getRuleContext(CharacterConstantContext.class,0);
		}
		public StringContext string() {
			return getRuleContext(StringContext.class,0);
		}
		public ArrayConstantContext arrayConstant() {
			return getRuleContext(ArrayConstantContext.class,0);
		}
		public LiteralContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_literal; }
	}

	public final LiteralContext literal() throws RecognitionException {
		LiteralContext _localctx = new LiteralContext(_ctx, getState());
		enterRule(_localctx, 14, RULE_literal);
		try {
			setState(90);
			_errHandler.sync(this);
			switch ( getInterpreter().adaptivePredict(_input,4,_ctx) ) {
			case 1:
				enterOuterAlt(_localctx, 1);
				{
				setState(85);
				match(NUMBER);
				}
				break;
			case 2:
				enterOuterAlt(_localctx, 2);
				{
				setState(86);
				symbolConstant();
				}
				break;
			case 3:
				enterOuterAlt(_localctx, 3);
				{
				setState(87);
				characterConstant();
				}
				break;
			case 4:
				enterOuterAlt(_localctx, 4);
				{
				setState(88);
				string();
				}
				break;
			case 5:
				enterOuterAlt(_localctx, 5);
				{
				setState(89);
				arrayConstant();
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
	public static class VariableNameContext extends ParserRuleContext {
		public TerminalNode IDENTIFIER() { return getToken(Smalltalk80Parser.IDENTIFIER, 0); }
		public VariableNameContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_variableName; }
	}

	public final VariableNameContext variableName() throws RecognitionException {
		VariableNameContext _localctx = new VariableNameContext(_ctx, getState());
		enterRule(_localctx, 16, RULE_variableName);
		try {
			enterOuterAlt(_localctx, 1);
			{
			setState(92);
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
	public static class UnarySelectorContext extends ParserRuleContext {
		public TerminalNode IDENTIFIER() { return getToken(Smalltalk80Parser.IDENTIFIER, 0); }
		public UnarySelectorContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_unarySelector; }
	}

	public final UnarySelectorContext unarySelector() throws RecognitionException {
		UnarySelectorContext _localctx = new UnarySelectorContext(_ctx, getState());
		enterRule(_localctx, 18, RULE_unarySelector);
		try {
			enterOuterAlt(_localctx, 1);
			{
			setState(94);
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
		public List<TerminalNode> SPECIALCHARACTER() { return getTokens(Smalltalk80Parser.SPECIALCHARACTER); }
		public TerminalNode SPECIALCHARACTER(int i) {
			return getToken(Smalltalk80Parser.SPECIALCHARACTER, i);
		}
		public BinarySelectorContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_binarySelector; }
	}

	public final BinarySelectorContext binarySelector() throws RecognitionException {
		BinarySelectorContext _localctx = new BinarySelectorContext(_ctx, getState());
		enterRule(_localctx, 20, RULE_binarySelector);
		try {
			setState(101);
			_errHandler.sync(this);
			switch (_input.LA(1)) {
			case T__6:
				enterOuterAlt(_localctx, 1);
				{
				setState(96);
				match(T__6);
				}
				break;
			case SPECIALCHARACTER:
				enterOuterAlt(_localctx, 2);
				{
				setState(97);
				match(SPECIALCHARACTER);
				setState(99);
				_errHandler.sync(this);
				switch ( getInterpreter().adaptivePredict(_input,5,_ctx) ) {
				case 1:
					{
					setState(98);
					match(SPECIALCHARACTER);
					}
					break;
				}
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
	public static class KeywordContext extends ParserRuleContext {
		public TerminalNode IDENTIFIER() { return getToken(Smalltalk80Parser.IDENTIFIER, 0); }
		public KeywordContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_keyword; }
	}

	public final KeywordContext keyword() throws RecognitionException {
		KeywordContext _localctx = new KeywordContext(_ctx, getState());
		enterRule(_localctx, 22, RULE_keyword);
		try {
			enterOuterAlt(_localctx, 1);
			{
			setState(103);
			match(IDENTIFIER);
			setState(104);
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
	public static class PrimaryContext extends ParserRuleContext {
		public VariableNameContext variableName() {
			return getRuleContext(VariableNameContext.class,0);
		}
		public LiteralContext literal() {
			return getRuleContext(LiteralContext.class,0);
		}
		public BlockContext block() {
			return getRuleContext(BlockContext.class,0);
		}
		public ExpressionContext expression() {
			return getRuleContext(ExpressionContext.class,0);
		}
		public PrimaryContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_primary; }
	}

	public final PrimaryContext primary() throws RecognitionException {
		PrimaryContext _localctx = new PrimaryContext(_ctx, getState());
		enterRule(_localctx, 24, RULE_primary);
		try {
			setState(113);
			_errHandler.sync(this);
			switch (_input.LA(1)) {
			case IDENTIFIER:
				enterOuterAlt(_localctx, 1);
				{
				setState(106);
				variableName();
				}
				break;
			case T__0:
			case T__1:
			case NUMBER:
			case STRING:
				enterOuterAlt(_localctx, 2);
				{
				setState(107);
				literal();
				}
				break;
			case T__10:
				enterOuterAlt(_localctx, 3);
				{
				setState(108);
				block();
				}
				break;
			case T__4:
				enterOuterAlt(_localctx, 4);
				{
				setState(109);
				match(T__4);
				setState(110);
				expression();
				setState(111);
				match(T__5);
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
	public static class UnaryObjectDescriptionContext extends ParserRuleContext {
		public PrimaryContext primary() {
			return getRuleContext(PrimaryContext.class,0);
		}
		public UnaryObjectDescriptionContext unaryObjectDescription() {
			return getRuleContext(UnaryObjectDescriptionContext.class,0);
		}
		public UnarySelectorContext unarySelector() {
			return getRuleContext(UnarySelectorContext.class,0);
		}
		public UnaryObjectDescriptionContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_unaryObjectDescription; }
	}

	public final UnaryObjectDescriptionContext unaryObjectDescription() throws RecognitionException {
		return unaryObjectDescription(0);
	}

	private UnaryObjectDescriptionContext unaryObjectDescription(int _p) throws RecognitionException {
		ParserRuleContext _parentctx = _ctx;
		int _parentState = getState();
		UnaryObjectDescriptionContext _localctx = new UnaryObjectDescriptionContext(_ctx, _parentState);
		UnaryObjectDescriptionContext _prevctx = _localctx;
		int _startState = 26;
		enterRecursionRule(_localctx, 26, RULE_unaryObjectDescription, _p);
		try {
			int _alt;
			enterOuterAlt(_localctx, 1);
			{
			{
			setState(116);
			primary();
			}
			_ctx.stop = _input.LT(-1);
			setState(122);
			_errHandler.sync(this);
			_alt = getInterpreter().adaptivePredict(_input,8,_ctx);
			while ( _alt!=2 && _alt!=org.antlr.v4.runtime.atn.ATN.INVALID_ALT_NUMBER ) {
				if ( _alt==1 ) {
					if ( _parseListeners!=null ) triggerExitRuleEvent();
					_prevctx = _localctx;
					{
					{
					_localctx = new UnaryObjectDescriptionContext(_parentctx, _parentState);
					pushNewRecursionContext(_localctx, _startState, RULE_unaryObjectDescription);
					setState(118);
					if (!(precpred(_ctx, 1))) throw new FailedPredicateException(this, "precpred(_ctx, 1)");
					setState(119);
					unarySelector();
					}
					} 
				}
				setState(124);
				_errHandler.sync(this);
				_alt = getInterpreter().adaptivePredict(_input,8,_ctx);
			}
			}
		}
		catch (RecognitionException re) {
			_localctx.exception = re;
			_errHandler.reportError(this, re);
			_errHandler.recover(this, re);
		}
		finally {
			unrollRecursionContexts(_parentctx);
		}
		return _localctx;
	}

	@SuppressWarnings("CheckReturnValue")
	public static class BinaryObjectDescriptionContext extends ParserRuleContext {
		public UnaryObjectDescriptionContext unaryObjectDescription() {
			return getRuleContext(UnaryObjectDescriptionContext.class,0);
		}
		public BinaryObjectDescriptionContext binaryObjectDescription() {
			return getRuleContext(BinaryObjectDescriptionContext.class,0);
		}
		public BinarySelectorContext binarySelector() {
			return getRuleContext(BinarySelectorContext.class,0);
		}
		public BinaryObjectDescriptionContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_binaryObjectDescription; }
	}

	public final BinaryObjectDescriptionContext binaryObjectDescription() throws RecognitionException {
		return binaryObjectDescription(0);
	}

	private BinaryObjectDescriptionContext binaryObjectDescription(int _p) throws RecognitionException {
		ParserRuleContext _parentctx = _ctx;
		int _parentState = getState();
		BinaryObjectDescriptionContext _localctx = new BinaryObjectDescriptionContext(_ctx, _parentState);
		BinaryObjectDescriptionContext _prevctx = _localctx;
		int _startState = 28;
		enterRecursionRule(_localctx, 28, RULE_binaryObjectDescription, _p);
		try {
			int _alt;
			enterOuterAlt(_localctx, 1);
			{
			{
			setState(126);
			unaryObjectDescription(0);
			}
			_ctx.stop = _input.LT(-1);
			setState(134);
			_errHandler.sync(this);
			_alt = getInterpreter().adaptivePredict(_input,9,_ctx);
			while ( _alt!=2 && _alt!=org.antlr.v4.runtime.atn.ATN.INVALID_ALT_NUMBER ) {
				if ( _alt==1 ) {
					if ( _parseListeners!=null ) triggerExitRuleEvent();
					_prevctx = _localctx;
					{
					{
					_localctx = new BinaryObjectDescriptionContext(_parentctx, _parentState);
					pushNewRecursionContext(_localctx, _startState, RULE_binaryObjectDescription);
					setState(128);
					if (!(precpred(_ctx, 1))) throw new FailedPredicateException(this, "precpred(_ctx, 1)");
					setState(129);
					binarySelector();
					setState(130);
					unaryObjectDescription(0);
					}
					} 
				}
				setState(136);
				_errHandler.sync(this);
				_alt = getInterpreter().adaptivePredict(_input,9,_ctx);
			}
			}
		}
		catch (RecognitionException re) {
			_localctx.exception = re;
			_errHandler.reportError(this, re);
			_errHandler.recover(this, re);
		}
		finally {
			unrollRecursionContexts(_parentctx);
		}
		return _localctx;
	}

	@SuppressWarnings("CheckReturnValue")
	public static class KeywordExpressionContext extends ParserRuleContext {
		public List<BinaryObjectDescriptionContext> binaryObjectDescription() {
			return getRuleContexts(BinaryObjectDescriptionContext.class);
		}
		public BinaryObjectDescriptionContext binaryObjectDescription(int i) {
			return getRuleContext(BinaryObjectDescriptionContext.class,i);
		}
		public List<KeywordContext> keyword() {
			return getRuleContexts(KeywordContext.class);
		}
		public KeywordContext keyword(int i) {
			return getRuleContext(KeywordContext.class,i);
		}
		public KeywordExpressionContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_keywordExpression; }
	}

	public final KeywordExpressionContext keywordExpression() throws RecognitionException {
		KeywordExpressionContext _localctx = new KeywordExpressionContext(_ctx, getState());
		enterRule(_localctx, 30, RULE_keywordExpression);
		int _la;
		try {
			enterOuterAlt(_localctx, 1);
			{
			setState(137);
			binaryObjectDescription(0);
			setState(141); 
			_errHandler.sync(this);
			_la = _input.LA(1);
			do {
				{
				{
				setState(138);
				keyword();
				setState(139);
				binaryObjectDescription(0);
				}
				}
				setState(143); 
				_errHandler.sync(this);
				_la = _input.LA(1);
			} while ( _la==IDENTIFIER );
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
	public static class MessageExpressionContext extends ParserRuleContext {
		public UnaryObjectDescriptionContext unaryObjectDescription() {
			return getRuleContext(UnaryObjectDescriptionContext.class,0);
		}
		public UnarySelectorContext unarySelector() {
			return getRuleContext(UnarySelectorContext.class,0);
		}
		public BinaryObjectDescriptionContext binaryObjectDescription() {
			return getRuleContext(BinaryObjectDescriptionContext.class,0);
		}
		public BinarySelectorContext binarySelector() {
			return getRuleContext(BinarySelectorContext.class,0);
		}
		public KeywordExpressionContext keywordExpression() {
			return getRuleContext(KeywordExpressionContext.class,0);
		}
		public MessageExpressionContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_messageExpression; }
	}

	public final MessageExpressionContext messageExpression() throws RecognitionException {
		MessageExpressionContext _localctx = new MessageExpressionContext(_ctx, getState());
		enterRule(_localctx, 32, RULE_messageExpression);
		try {
			setState(153);
			_errHandler.sync(this);
			switch ( getInterpreter().adaptivePredict(_input,11,_ctx) ) {
			case 1:
				enterOuterAlt(_localctx, 1);
				{
				setState(145);
				unaryObjectDescription(0);
				setState(146);
				unarySelector();
				}
				break;
			case 2:
				enterOuterAlt(_localctx, 2);
				{
				setState(148);
				binaryObjectDescription(0);
				setState(149);
				binarySelector();
				setState(150);
				unaryObjectDescription(0);
				}
				break;
			case 3:
				enterOuterAlt(_localctx, 3);
				{
				setState(152);
				keywordExpression();
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
	public static class CascadedMessageExpressionContext extends ParserRuleContext {
		public MessageExpressionContext messageExpression() {
			return getRuleContext(MessageExpressionContext.class,0);
		}
		public List<UnarySelectorContext> unarySelector() {
			return getRuleContexts(UnarySelectorContext.class);
		}
		public UnarySelectorContext unarySelector(int i) {
			return getRuleContext(UnarySelectorContext.class,i);
		}
		public List<BinarySelectorContext> binarySelector() {
			return getRuleContexts(BinarySelectorContext.class);
		}
		public BinarySelectorContext binarySelector(int i) {
			return getRuleContext(BinarySelectorContext.class,i);
		}
		public List<UnaryObjectDescriptionContext> unaryObjectDescription() {
			return getRuleContexts(UnaryObjectDescriptionContext.class);
		}
		public UnaryObjectDescriptionContext unaryObjectDescription(int i) {
			return getRuleContext(UnaryObjectDescriptionContext.class,i);
		}
		public List<KeywordContext> keyword() {
			return getRuleContexts(KeywordContext.class);
		}
		public KeywordContext keyword(int i) {
			return getRuleContext(KeywordContext.class,i);
		}
		public List<BinaryObjectDescriptionContext> binaryObjectDescription() {
			return getRuleContexts(BinaryObjectDescriptionContext.class);
		}
		public BinaryObjectDescriptionContext binaryObjectDescription(int i) {
			return getRuleContext(BinaryObjectDescriptionContext.class,i);
		}
		public CascadedMessageExpressionContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_cascadedMessageExpression; }
	}

	public final CascadedMessageExpressionContext cascadedMessageExpression() throws RecognitionException {
		CascadedMessageExpressionContext _localctx = new CascadedMessageExpressionContext(_ctx, getState());
		enterRule(_localctx, 34, RULE_cascadedMessageExpression);
		int _la;
		try {
			int _alt;
			enterOuterAlt(_localctx, 1);
			{
			setState(155);
			messageExpression();
			setState(170); 
			_errHandler.sync(this);
			_alt = 1;
			do {
				switch (_alt) {
				case 1:
					{
					{
					setState(156);
					match(T__8);
					setState(168);
					_errHandler.sync(this);
					switch ( getInterpreter().adaptivePredict(_input,13,_ctx) ) {
					case 1:
						{
						setState(157);
						unarySelector();
						}
						break;
					case 2:
						{
						setState(158);
						binarySelector();
						setState(159);
						unaryObjectDescription(0);
						}
						break;
					case 3:
						{
						setState(164); 
						_errHandler.sync(this);
						_la = _input.LA(1);
						do {
							{
							{
							setState(161);
							keyword();
							setState(162);
							binaryObjectDescription(0);
							}
							}
							setState(166); 
							_errHandler.sync(this);
							_la = _input.LA(1);
						} while ( _la==IDENTIFIER );
						}
						break;
					}
					}
					}
					break;
				default:
					throw new NoViableAltException(this);
				}
				setState(172); 
				_errHandler.sync(this);
				_alt = getInterpreter().adaptivePredict(_input,14,_ctx);
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
	public static class ExpressionContext extends ParserRuleContext {
		public PrimaryContext primary() {
			return getRuleContext(PrimaryContext.class,0);
		}
		public MessageExpressionContext messageExpression() {
			return getRuleContext(MessageExpressionContext.class,0);
		}
		public CascadedMessageExpressionContext cascadedMessageExpression() {
			return getRuleContext(CascadedMessageExpressionContext.class,0);
		}
		public List<VariableNameContext> variableName() {
			return getRuleContexts(VariableNameContext.class);
		}
		public VariableNameContext variableName(int i) {
			return getRuleContext(VariableNameContext.class,i);
		}
		public List<TerminalNode> ASSIGN_ARROW() { return getTokens(Smalltalk80Parser.ASSIGN_ARROW); }
		public TerminalNode ASSIGN_ARROW(int i) {
			return getToken(Smalltalk80Parser.ASSIGN_ARROW, i);
		}
		public ExpressionContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_expression; }
	}

	public final ExpressionContext expression() throws RecognitionException {
		ExpressionContext _localctx = new ExpressionContext(_ctx, getState());
		enterRule(_localctx, 36, RULE_expression);
		try {
			int _alt;
			enterOuterAlt(_localctx, 1);
			{
			setState(179);
			_errHandler.sync(this);
			_alt = getInterpreter().adaptivePredict(_input,15,_ctx);
			while ( _alt!=2 && _alt!=org.antlr.v4.runtime.atn.ATN.INVALID_ALT_NUMBER ) {
				if ( _alt==1 ) {
					{
					{
					setState(174);
					variableName();
					setState(175);
					match(ASSIGN_ARROW);
					}
					} 
				}
				setState(181);
				_errHandler.sync(this);
				_alt = getInterpreter().adaptivePredict(_input,15,_ctx);
			}
			setState(185);
			_errHandler.sync(this);
			switch ( getInterpreter().adaptivePredict(_input,16,_ctx) ) {
			case 1:
				{
				setState(182);
				primary();
				}
				break;
			case 2:
				{
				setState(183);
				messageExpression();
				}
				break;
			case 3:
				{
				setState(184);
				cascadedMessageExpression();
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
	public static class StatementsContext extends ParserRuleContext {
		public List<StatementContext> statement() {
			return getRuleContexts(StatementContext.class);
		}
		public StatementContext statement(int i) {
			return getRuleContext(StatementContext.class,i);
		}
		public StatementsContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_statements; }
	}

	public final StatementsContext statements() throws RecognitionException {
		StatementsContext _localctx = new StatementsContext(_ctx, getState());
		enterRule(_localctx, 38, RULE_statements);
		int _la;
		try {
			enterOuterAlt(_localctx, 1);
			{
			setState(195);
			_errHandler.sync(this);
			_la = _input.LA(1);
			if ((((_la) & ~0x3f) == 0 && ((1L << _la) & 920614L) != 0)) {
				{
				setState(187);
				statement();
				setState(192);
				_errHandler.sync(this);
				_la = _input.LA(1);
				while (_la==T__8) {
					{
					{
					setState(188);
					match(T__8);
					setState(189);
					statement();
					}
					}
					setState(194);
					_errHandler.sync(this);
					_la = _input.LA(1);
				}
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
	public static class StatementContext extends ParserRuleContext {
		public ExpressionContext expression() {
			return getRuleContext(ExpressionContext.class,0);
		}
		public StatementContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_statement; }
	}

	public final StatementContext statement() throws RecognitionException {
		StatementContext _localctx = new StatementContext(_ctx, getState());
		enterRule(_localctx, 40, RULE_statement);
		try {
			setState(200);
			_errHandler.sync(this);
			switch (_input.LA(1)) {
			case T__9:
				enterOuterAlt(_localctx, 1);
				{
				setState(197);
				match(T__9);
				setState(198);
				expression();
				}
				break;
			case T__0:
			case T__1:
			case T__4:
			case T__10:
			case NUMBER:
			case IDENTIFIER:
			case STRING:
				enterOuterAlt(_localctx, 2);
				{
				setState(199);
				expression();
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
	public static class BlockContext extends ParserRuleContext {
		public StatementsContext statements() {
			return getRuleContext(StatementsContext.class,0);
		}
		public List<VariableNameContext> variableName() {
			return getRuleContexts(VariableNameContext.class);
		}
		public VariableNameContext variableName(int i) {
			return getRuleContext(VariableNameContext.class,i);
		}
		public BlockContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_block; }
	}

	public final BlockContext block() throws RecognitionException {
		BlockContext _localctx = new BlockContext(_ctx, getState());
		enterRule(_localctx, 42, RULE_block);
		int _la;
		try {
			setState(218);
			_errHandler.sync(this);
			switch ( getInterpreter().adaptivePredict(_input,22,_ctx) ) {
			case 1:
				enterOuterAlt(_localctx, 1);
				{
				setState(202);
				match(T__10);
				setState(204);
				_errHandler.sync(this);
				switch ( getInterpreter().adaptivePredict(_input,20,_ctx) ) {
				case 1:
					{
					setState(203);
					statements();
					}
					break;
				}
				setState(206);
				match(T__11);
				}
				break;
			case 2:
				enterOuterAlt(_localctx, 2);
				{
				setState(207);
				match(T__10);
				setState(210); 
				_errHandler.sync(this);
				_la = _input.LA(1);
				do {
					{
					{
					setState(208);
					match(T__7);
					setState(209);
					variableName();
					}
					}
					setState(212); 
					_errHandler.sync(this);
					_la = _input.LA(1);
				} while ( _la==T__7 );
				setState(214);
				match(T__12);
				setState(215);
				statements();
				setState(216);
				match(T__11);
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
	public static class TemporariesContext extends ParserRuleContext {
		public List<VariableNameContext> variableName() {
			return getRuleContexts(VariableNameContext.class);
		}
		public VariableNameContext variableName(int i) {
			return getRuleContext(VariableNameContext.class,i);
		}
		public TemporariesContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_temporaries; }
	}

	public final TemporariesContext temporaries() throws RecognitionException {
		TemporariesContext _localctx = new TemporariesContext(_ctx, getState());
		enterRule(_localctx, 44, RULE_temporaries);
		int _la;
		try {
			enterOuterAlt(_localctx, 1);
			{
			setState(220);
			match(T__12);
			setState(224);
			_errHandler.sync(this);
			_la = _input.LA(1);
			while (_la==IDENTIFIER) {
				{
				{
				setState(221);
				variableName();
				}
				}
				setState(226);
				_errHandler.sync(this);
				_la = _input.LA(1);
			}
			setState(227);
			match(T__12);
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
	public static class MethodContext extends ParserRuleContext {
		public MessagePatternContext messagePattern() {
			return getRuleContext(MessagePatternContext.class,0);
		}
		public TemporariesContext temporaries() {
			return getRuleContext(TemporariesContext.class,0);
		}
		public StatementsContext statements() {
			return getRuleContext(StatementsContext.class,0);
		}
		public MethodContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_method; }
	}

	public final MethodContext method() throws RecognitionException {
		MethodContext _localctx = new MethodContext(_ctx, getState());
		enterRule(_localctx, 46, RULE_method);
		try {
			setState(237);
			_errHandler.sync(this);
			switch ( getInterpreter().adaptivePredict(_input,24,_ctx) ) {
			case 1:
				enterOuterAlt(_localctx, 1);
				{
				setState(229);
				messagePattern();
				}
				break;
			case 2:
				enterOuterAlt(_localctx, 2);
				{
				setState(230);
				messagePattern();
				setState(231);
				temporaries();
				setState(232);
				statements();
				}
				break;
			case 3:
				enterOuterAlt(_localctx, 3);
				{
				setState(234);
				messagePattern();
				setState(235);
				statements();
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
	public static class MessagePatternContext extends ParserRuleContext {
		public UnarySelectorContext unarySelector() {
			return getRuleContext(UnarySelectorContext.class,0);
		}
		public BinarySelectorContext binarySelector() {
			return getRuleContext(BinarySelectorContext.class,0);
		}
		public List<VariableNameContext> variableName() {
			return getRuleContexts(VariableNameContext.class);
		}
		public VariableNameContext variableName(int i) {
			return getRuleContext(VariableNameContext.class,i);
		}
		public List<KeywordContext> keyword() {
			return getRuleContexts(KeywordContext.class);
		}
		public KeywordContext keyword(int i) {
			return getRuleContext(KeywordContext.class,i);
		}
		public MessagePatternContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_messagePattern; }
	}

	public final MessagePatternContext messagePattern() throws RecognitionException {
		MessagePatternContext _localctx = new MessagePatternContext(_ctx, getState());
		enterRule(_localctx, 48, RULE_messagePattern);
		try {
			int _alt;
			setState(250);
			_errHandler.sync(this);
			switch ( getInterpreter().adaptivePredict(_input,26,_ctx) ) {
			case 1:
				enterOuterAlt(_localctx, 1);
				{
				setState(239);
				unarySelector();
				}
				break;
			case 2:
				enterOuterAlt(_localctx, 2);
				{
				setState(240);
				binarySelector();
				setState(241);
				variableName();
				}
				break;
			case 3:
				enterOuterAlt(_localctx, 3);
				{
				setState(246); 
				_errHandler.sync(this);
				_alt = 1;
				do {
					switch (_alt) {
					case 1:
						{
						{
						setState(243);
						keyword();
						setState(244);
						variableName();
						}
						}
						break;
					default:
						throw new NoViableAltException(this);
					}
					setState(248); 
					_errHandler.sync(this);
					_alt = getInterpreter().adaptivePredict(_input,25,_ctx);
				} while ( _alt!=2 && _alt!=org.antlr.v4.runtime.atn.ATN.INVALID_ALT_NUMBER );
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

	public boolean sempred(RuleContext _localctx, int ruleIndex, int predIndex) {
		switch (ruleIndex) {
		case 13:
			return unaryObjectDescription_sempred((UnaryObjectDescriptionContext)_localctx, predIndex);
		case 14:
			return binaryObjectDescription_sempred((BinaryObjectDescriptionContext)_localctx, predIndex);
		}
		return true;
	}
	private boolean unaryObjectDescription_sempred(UnaryObjectDescriptionContext _localctx, int predIndex) {
		switch (predIndex) {
		case 0:
			return precpred(_ctx, 1);
		}
		return true;
	}
	private boolean binaryObjectDescription_sempred(BinaryObjectDescriptionContext _localctx, int predIndex) {
		switch (predIndex) {
		case 1:
			return precpred(_ctx, 1);
		}
		return true;
	}

	public static final String _serializedATN =
		"\u0004\u0001\u0015\u00fd\u0002\u0000\u0007\u0000\u0002\u0001\u0007\u0001"+
		"\u0002\u0002\u0007\u0002\u0002\u0003\u0007\u0003\u0002\u0004\u0007\u0004"+
		"\u0002\u0005\u0007\u0005\u0002\u0006\u0007\u0006\u0002\u0007\u0007\u0007"+
		"\u0002\b\u0007\b\u0002\t\u0007\t\u0002\n\u0007\n\u0002\u000b\u0007\u000b"+
		"\u0002\f\u0007\f\u0002\r\u0007\r\u0002\u000e\u0007\u000e\u0002\u000f\u0007"+
		"\u000f\u0002\u0010\u0007\u0010\u0002\u0011\u0007\u0011\u0002\u0012\u0007"+
		"\u0012\u0002\u0013\u0007\u0013\u0002\u0014\u0007\u0014\u0002\u0015\u0007"+
		"\u0015\u0002\u0016\u0007\u0016\u0002\u0017\u0007\u0017\u0002\u0018\u0007"+
		"\u0018\u0001\u0000\u0001\u0000\u0001\u0001\u0001\u0001\u0001\u0002\u0001"+
		"\u0002\u0001\u0002\u0004\u0002:\b\u0002\u000b\u0002\f\u0002;\u0003\u0002"+
		">\b\u0002\u0001\u0003\u0001\u0003\u0001\u0003\u0001\u0004\u0001\u0004"+
		"\u0001\u0004\u0001\u0005\u0001\u0005\u0001\u0005\u0001\u0005\u0001\u0005"+
		"\u0001\u0005\u0005\u0005L\b\u0005\n\u0005\f\u0005O\t\u0005\u0001\u0005"+
		"\u0001\u0005\u0001\u0006\u0001\u0006\u0001\u0006\u0001\u0007\u0001\u0007"+
		"\u0001\u0007\u0001\u0007\u0001\u0007\u0003\u0007[\b\u0007\u0001\b\u0001"+
		"\b\u0001\t\u0001\t\u0001\n\u0001\n\u0001\n\u0003\nd\b\n\u0003\nf\b\n\u0001"+
		"\u000b\u0001\u000b\u0001\u000b\u0001\f\u0001\f\u0001\f\u0001\f\u0001\f"+
		"\u0001\f\u0001\f\u0003\fr\b\f\u0001\r\u0001\r\u0001\r\u0001\r\u0001\r"+
		"\u0005\ry\b\r\n\r\f\r|\t\r\u0001\u000e\u0001\u000e\u0001\u000e\u0001\u000e"+
		"\u0001\u000e\u0001\u000e\u0001\u000e\u0005\u000e\u0085\b\u000e\n\u000e"+
		"\f\u000e\u0088\t\u000e\u0001\u000f\u0001\u000f\u0001\u000f\u0001\u000f"+
		"\u0004\u000f\u008e\b\u000f\u000b\u000f\f\u000f\u008f\u0001\u0010\u0001"+
		"\u0010\u0001\u0010\u0001\u0010\u0001\u0010\u0001\u0010\u0001\u0010\u0001"+
		"\u0010\u0003\u0010\u009a\b\u0010\u0001\u0011\u0001\u0011\u0001\u0011\u0001"+
		"\u0011\u0001\u0011\u0001\u0011\u0001\u0011\u0001\u0011\u0001\u0011\u0004"+
		"\u0011\u00a5\b\u0011\u000b\u0011\f\u0011\u00a6\u0003\u0011\u00a9\b\u0011"+
		"\u0004\u0011\u00ab\b\u0011\u000b\u0011\f\u0011\u00ac\u0001\u0012\u0001"+
		"\u0012\u0001\u0012\u0005\u0012\u00b2\b\u0012\n\u0012\f\u0012\u00b5\t\u0012"+
		"\u0001\u0012\u0001\u0012\u0001\u0012\u0003\u0012\u00ba\b\u0012\u0001\u0013"+
		"\u0001\u0013\u0001\u0013\u0005\u0013\u00bf\b\u0013\n\u0013\f\u0013\u00c2"+
		"\t\u0013\u0003\u0013\u00c4\b\u0013\u0001\u0014\u0001\u0014\u0001\u0014"+
		"\u0003\u0014\u00c9\b\u0014\u0001\u0015\u0001\u0015\u0003\u0015\u00cd\b"+
		"\u0015\u0001\u0015\u0001\u0015\u0001\u0015\u0001\u0015\u0004\u0015\u00d3"+
		"\b\u0015\u000b\u0015\f\u0015\u00d4\u0001\u0015\u0001\u0015\u0001\u0015"+
		"\u0001\u0015\u0003\u0015\u00db\b\u0015\u0001\u0016\u0001\u0016\u0005\u0016"+
		"\u00df\b\u0016\n\u0016\f\u0016\u00e2\t\u0016\u0001\u0016\u0001\u0016\u0001"+
		"\u0017\u0001\u0017\u0001\u0017\u0001\u0017\u0001\u0017\u0001\u0017\u0001"+
		"\u0017\u0001\u0017\u0003\u0017\u00ee\b\u0017\u0001\u0018\u0001\u0018\u0001"+
		"\u0018\u0001\u0018\u0001\u0018\u0001\u0018\u0001\u0018\u0004\u0018\u00f7"+
		"\b\u0018\u000b\u0018\f\u0018\u00f8\u0003\u0018\u00fb\b\u0018\u0001\u0018"+
		"\u0000\u0002\u001a\u001c\u0019\u0000\u0002\u0004\u0006\b\n\f\u000e\u0010"+
		"\u0012\u0014\u0016\u0018\u001a\u001c\u001e \"$&(*,.0\u0000\u0001\u0002"+
		"\u0000\u0003\u0004\u000f\u000f\u010c\u00002\u0001\u0000\u0000\u0000\u0002"+
		"4\u0001\u0000\u0000\u0000\u0004=\u0001\u0000\u0000\u0000\u0006?\u0001"+
		"\u0000\u0000\u0000\bB\u0001\u0000\u0000\u0000\nE\u0001\u0000\u0000\u0000"+
		"\fR\u0001\u0000\u0000\u0000\u000eZ\u0001\u0000\u0000\u0000\u0010\\\u0001"+
		"\u0000\u0000\u0000\u0012^\u0001\u0000\u0000\u0000\u0014e\u0001\u0000\u0000"+
		"\u0000\u0016g\u0001\u0000\u0000\u0000\u0018q\u0001\u0000\u0000\u0000\u001a"+
		"s\u0001\u0000\u0000\u0000\u001c}\u0001\u0000\u0000\u0000\u001e\u0089\u0001"+
		"\u0000\u0000\u0000 \u0099\u0001\u0000\u0000\u0000\"\u009b\u0001\u0000"+
		"\u0000\u0000$\u00b3\u0001\u0000\u0000\u0000&\u00c3\u0001\u0000\u0000\u0000"+
		"(\u00c8\u0001\u0000\u0000\u0000*\u00da\u0001\u0000\u0000\u0000,\u00dc"+
		"\u0001\u0000\u0000\u0000.\u00ed\u0001\u0000\u0000\u00000\u00fa\u0001\u0000"+
		"\u0000\u000023\u0005\u0012\u0000\u00003\u0001\u0001\u0000\u0000\u0000"+
		"45\u0005\u0013\u0000\u00005\u0003\u0001\u0000\u0000\u00006>\u0003\u0000"+
		"\u0000\u00007>\u0003\u0014\n\u00008:\u0003\u0016\u000b\u000098\u0001\u0000"+
		"\u0000\u0000:;\u0001\u0000\u0000\u0000;9\u0001\u0000\u0000\u0000;<\u0001"+
		"\u0000\u0000\u0000<>\u0001\u0000\u0000\u0000=6\u0001\u0000\u0000\u0000"+
		"=7\u0001\u0000\u0000\u0000=9\u0001\u0000\u0000\u0000>\u0005\u0001\u0000"+
		"\u0000\u0000?@\u0005\u0001\u0000\u0000@A\u0003\u0004\u0002\u0000A\u0007"+
		"\u0001\u0000\u0000\u0000BC\u0005\u0002\u0000\u0000CD\u0007\u0000\u0000"+
		"\u0000D\t\u0001\u0000\u0000\u0000EM\u0005\u0005\u0000\u0000FL\u0005\u0011"+
		"\u0000\u0000GL\u0003\u0004\u0002\u0000HL\u0003\u0002\u0001\u0000IL\u0003"+
		"\b\u0004\u0000JL\u0003\n\u0005\u0000KF\u0001\u0000\u0000\u0000KG\u0001"+
		"\u0000\u0000\u0000KH\u0001\u0000\u0000\u0000KI\u0001\u0000\u0000\u0000"+
		"KJ\u0001\u0000\u0000\u0000LO\u0001\u0000\u0000\u0000MK\u0001\u0000\u0000"+
		"\u0000MN\u0001\u0000\u0000\u0000NP\u0001\u0000\u0000\u0000OM\u0001\u0000"+
		"\u0000\u0000PQ\u0005\u0006\u0000\u0000Q\u000b\u0001\u0000\u0000\u0000"+
		"RS\u0005\u0001\u0000\u0000ST\u0003\n\u0005\u0000T\r\u0001\u0000\u0000"+
		"\u0000U[\u0005\u0011\u0000\u0000V[\u0003\u0006\u0003\u0000W[\u0003\b\u0004"+
		"\u0000X[\u0003\u0002\u0001\u0000Y[\u0003\f\u0006\u0000ZU\u0001\u0000\u0000"+
		"\u0000ZV\u0001\u0000\u0000\u0000ZW\u0001\u0000\u0000\u0000ZX\u0001\u0000"+
		"\u0000\u0000ZY\u0001\u0000\u0000\u0000[\u000f\u0001\u0000\u0000\u0000"+
		"\\]\u0005\u0012\u0000\u0000]\u0011\u0001\u0000\u0000\u0000^_\u0005\u0012"+
		"\u0000\u0000_\u0013\u0001\u0000\u0000\u0000`f\u0005\u0007\u0000\u0000"+
		"ac\u0005\u000e\u0000\u0000bd\u0005\u000e\u0000\u0000cb\u0001\u0000\u0000"+
		"\u0000cd\u0001\u0000\u0000\u0000df\u0001\u0000\u0000\u0000e`\u0001\u0000"+
		"\u0000\u0000ea\u0001\u0000\u0000\u0000f\u0015\u0001\u0000\u0000\u0000"+
		"gh\u0005\u0012\u0000\u0000hi\u0005\b\u0000\u0000i\u0017\u0001\u0000\u0000"+
		"\u0000jr\u0003\u0010\b\u0000kr\u0003\u000e\u0007\u0000lr\u0003*\u0015"+
		"\u0000mn\u0005\u0005\u0000\u0000no\u0003$\u0012\u0000op\u0005\u0006\u0000"+
		"\u0000pr\u0001\u0000\u0000\u0000qj\u0001\u0000\u0000\u0000qk\u0001\u0000"+
		"\u0000\u0000ql\u0001\u0000\u0000\u0000qm\u0001\u0000\u0000\u0000r\u0019"+
		"\u0001\u0000\u0000\u0000st\u0006\r\uffff\uffff\u0000tu\u0003\u0018\f\u0000"+
		"uz\u0001\u0000\u0000\u0000vw\n\u0001\u0000\u0000wy\u0003\u0012\t\u0000"+
		"xv\u0001\u0000\u0000\u0000y|\u0001\u0000\u0000\u0000zx\u0001\u0000\u0000"+
		"\u0000z{\u0001\u0000\u0000\u0000{\u001b\u0001\u0000\u0000\u0000|z\u0001"+
		"\u0000\u0000\u0000}~\u0006\u000e\uffff\uffff\u0000~\u007f\u0003\u001a"+
		"\r\u0000\u007f\u0086\u0001\u0000\u0000\u0000\u0080\u0081\n\u0001\u0000"+
		"\u0000\u0081\u0082\u0003\u0014\n\u0000\u0082\u0083\u0003\u001a\r\u0000"+
		"\u0083\u0085\u0001\u0000\u0000\u0000\u0084\u0080\u0001\u0000\u0000\u0000"+
		"\u0085\u0088\u0001\u0000\u0000\u0000\u0086\u0084\u0001\u0000\u0000\u0000"+
		"\u0086\u0087\u0001\u0000\u0000\u0000\u0087\u001d\u0001\u0000\u0000\u0000"+
		"\u0088\u0086\u0001\u0000\u0000\u0000\u0089\u008d\u0003\u001c\u000e\u0000"+
		"\u008a\u008b\u0003\u0016\u000b\u0000\u008b\u008c\u0003\u001c\u000e\u0000"+
		"\u008c\u008e\u0001\u0000\u0000\u0000\u008d\u008a\u0001\u0000\u0000\u0000"+
		"\u008e\u008f\u0001\u0000\u0000\u0000\u008f\u008d\u0001\u0000\u0000\u0000"+
		"\u008f\u0090\u0001\u0000\u0000\u0000\u0090\u001f\u0001\u0000\u0000\u0000"+
		"\u0091\u0092\u0003\u001a\r\u0000\u0092\u0093\u0003\u0012\t\u0000\u0093"+
		"\u009a\u0001\u0000\u0000\u0000\u0094\u0095\u0003\u001c\u000e\u0000\u0095"+
		"\u0096\u0003\u0014\n\u0000\u0096\u0097\u0003\u001a\r\u0000\u0097\u009a"+
		"\u0001\u0000\u0000\u0000\u0098\u009a\u0003\u001e\u000f\u0000\u0099\u0091"+
		"\u0001\u0000\u0000\u0000\u0099\u0094\u0001\u0000\u0000\u0000\u0099\u0098"+
		"\u0001\u0000\u0000\u0000\u009a!\u0001\u0000\u0000\u0000\u009b\u00aa\u0003"+
		" \u0010\u0000\u009c\u00a8\u0005\t\u0000\u0000\u009d\u00a9\u0003\u0012"+
		"\t\u0000\u009e\u009f\u0003\u0014\n\u0000\u009f\u00a0\u0003\u001a\r\u0000"+
		"\u00a0\u00a9\u0001\u0000\u0000\u0000\u00a1\u00a2\u0003\u0016\u000b\u0000"+
		"\u00a2\u00a3\u0003\u001c\u000e\u0000\u00a3\u00a5\u0001\u0000\u0000\u0000"+
		"\u00a4\u00a1\u0001\u0000\u0000\u0000\u00a5\u00a6\u0001\u0000\u0000\u0000"+
		"\u00a6\u00a4\u0001\u0000\u0000\u0000\u00a6\u00a7\u0001\u0000\u0000\u0000"+
		"\u00a7\u00a9\u0001\u0000\u0000\u0000\u00a8\u009d\u0001\u0000\u0000\u0000"+
		"\u00a8\u009e\u0001\u0000\u0000\u0000\u00a8\u00a4\u0001\u0000\u0000\u0000"+
		"\u00a9\u00ab\u0001\u0000\u0000\u0000\u00aa\u009c\u0001\u0000\u0000\u0000"+
		"\u00ab\u00ac\u0001\u0000\u0000\u0000\u00ac\u00aa\u0001\u0000\u0000\u0000"+
		"\u00ac\u00ad\u0001\u0000\u0000\u0000\u00ad#\u0001\u0000\u0000\u0000\u00ae"+
		"\u00af\u0003\u0010\b\u0000\u00af\u00b0\u0005\u0010\u0000\u0000\u00b0\u00b2"+
		"\u0001\u0000\u0000\u0000\u00b1\u00ae\u0001\u0000\u0000\u0000\u00b2\u00b5"+
		"\u0001\u0000\u0000\u0000\u00b3\u00b1\u0001\u0000\u0000\u0000\u00b3\u00b4"+
		"\u0001\u0000\u0000\u0000\u00b4\u00b9\u0001\u0000\u0000\u0000\u00b5\u00b3"+
		"\u0001\u0000\u0000\u0000\u00b6\u00ba\u0003\u0018\f\u0000\u00b7\u00ba\u0003"+
		" \u0010\u0000\u00b8\u00ba\u0003\"\u0011\u0000\u00b9\u00b6\u0001\u0000"+
		"\u0000\u0000\u00b9\u00b7\u0001\u0000\u0000\u0000\u00b9\u00b8\u0001\u0000"+
		"\u0000\u0000\u00ba%\u0001\u0000\u0000\u0000\u00bb\u00c0\u0003(\u0014\u0000"+
		"\u00bc\u00bd\u0005\t\u0000\u0000\u00bd\u00bf\u0003(\u0014\u0000\u00be"+
		"\u00bc\u0001\u0000\u0000\u0000\u00bf\u00c2\u0001\u0000\u0000\u0000\u00c0"+
		"\u00be\u0001\u0000\u0000\u0000\u00c0\u00c1\u0001\u0000\u0000\u0000\u00c1"+
		"\u00c4\u0001\u0000\u0000\u0000\u00c2\u00c0\u0001\u0000\u0000\u0000\u00c3"+
		"\u00bb\u0001\u0000\u0000\u0000\u00c3\u00c4\u0001\u0000\u0000\u0000\u00c4"+
		"\'\u0001\u0000\u0000\u0000\u00c5\u00c6\u0005\n\u0000\u0000\u00c6\u00c9"+
		"\u0003$\u0012\u0000\u00c7\u00c9\u0003$\u0012\u0000\u00c8\u00c5\u0001\u0000"+
		"\u0000\u0000\u00c8\u00c7\u0001\u0000\u0000\u0000\u00c9)\u0001\u0000\u0000"+
		"\u0000\u00ca\u00cc\u0005\u000b\u0000\u0000\u00cb\u00cd\u0003&\u0013\u0000"+
		"\u00cc\u00cb\u0001\u0000\u0000\u0000\u00cc\u00cd\u0001\u0000\u0000\u0000"+
		"\u00cd\u00ce\u0001\u0000\u0000\u0000\u00ce\u00db\u0005\f\u0000\u0000\u00cf"+
		"\u00d2\u0005\u000b\u0000\u0000\u00d0\u00d1\u0005\b\u0000\u0000\u00d1\u00d3"+
		"\u0003\u0010\b\u0000\u00d2\u00d0\u0001\u0000\u0000\u0000\u00d3\u00d4\u0001"+
		"\u0000\u0000\u0000\u00d4\u00d2\u0001\u0000\u0000\u0000\u00d4\u00d5\u0001"+
		"\u0000\u0000\u0000\u00d5\u00d6\u0001\u0000\u0000\u0000\u00d6\u00d7\u0005"+
		"\r\u0000\u0000\u00d7\u00d8\u0003&\u0013\u0000\u00d8\u00d9\u0005\f\u0000"+
		"\u0000\u00d9\u00db\u0001\u0000\u0000\u0000\u00da\u00ca\u0001\u0000\u0000"+
		"\u0000\u00da\u00cf\u0001\u0000\u0000\u0000\u00db+\u0001\u0000\u0000\u0000"+
		"\u00dc\u00e0\u0005\r\u0000\u0000\u00dd\u00df\u0003\u0010\b\u0000\u00de"+
		"\u00dd\u0001\u0000\u0000\u0000\u00df\u00e2\u0001\u0000\u0000\u0000\u00e0"+
		"\u00de\u0001\u0000\u0000\u0000\u00e0\u00e1\u0001\u0000\u0000\u0000\u00e1"+
		"\u00e3\u0001\u0000\u0000\u0000\u00e2\u00e0\u0001\u0000\u0000\u0000\u00e3"+
		"\u00e4\u0005\r\u0000\u0000\u00e4-\u0001\u0000\u0000\u0000\u00e5\u00ee"+
		"\u00030\u0018\u0000\u00e6\u00e7\u00030\u0018\u0000\u00e7\u00e8\u0003,"+
		"\u0016\u0000\u00e8\u00e9\u0003&\u0013\u0000\u00e9\u00ee\u0001\u0000\u0000"+
		"\u0000\u00ea\u00eb\u00030\u0018\u0000\u00eb\u00ec\u0003&\u0013\u0000\u00ec"+
		"\u00ee\u0001\u0000\u0000\u0000\u00ed\u00e5\u0001\u0000\u0000\u0000\u00ed"+
		"\u00e6\u0001\u0000\u0000\u0000\u00ed\u00ea\u0001\u0000\u0000\u0000\u00ee"+
		"/\u0001\u0000\u0000\u0000\u00ef\u00fb\u0003\u0012\t\u0000\u00f0\u00f1"+
		"\u0003\u0014\n\u0000\u00f1\u00f2\u0003\u0010\b\u0000\u00f2\u00fb\u0001"+
		"\u0000\u0000\u0000\u00f3\u00f4\u0003\u0016\u000b\u0000\u00f4\u00f5\u0003"+
		"\u0010\b\u0000\u00f5\u00f7\u0001\u0000\u0000\u0000\u00f6\u00f3\u0001\u0000"+
		"\u0000\u0000\u00f7\u00f8\u0001\u0000\u0000\u0000\u00f8\u00f6\u0001\u0000"+
		"\u0000\u0000\u00f8\u00f9\u0001\u0000\u0000\u0000\u00f9\u00fb\u0001\u0000"+
		"\u0000\u0000\u00fa\u00ef\u0001\u0000\u0000\u0000\u00fa\u00f0\u0001\u0000"+
		"\u0000\u0000\u00fa\u00f6\u0001\u0000\u0000\u0000\u00fb1\u0001\u0000\u0000"+
		"\u0000\u001b;=KMZceqz\u0086\u008f\u0099\u00a6\u00a8\u00ac\u00b3\u00b9"+
		"\u00c0\u00c3\u00c8\u00cc\u00d4\u00da\u00e0\u00ed\u00f8\u00fa";
	public static final ATN _ATN =
		new ATNDeserializer().deserialize(_serializedATN.toCharArray());
	static {
		_decisionToDFA = new DFA[_ATN.getNumberOfDecisions()];
		for (int i = 0; i < _ATN.getNumberOfDecisions(); i++) {
			_decisionToDFA[i] = new DFA(_ATN.getDecisionState(i), i);
		}
	}
}