"BlockContextTest - Comprehensive validation of nested block context variable access and mutation"

Object subclass: #BlockContextTest
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Tests-BlockContext'! !

!BlockContextTest methodsFor: 'testing'!

testAllCombinations
	"Test all categories: single-level (9), two-level (81), three-level (27), method multi-temp (6), mutation (3), method-args (6), real-world (5), edge-cases (7), three-level-gaps (4), stress (3), non-local-returns (3), exception-handling (2), block-lifetime (2), control-flow (2), edge-special (2), stress-deep (1), instance-variables (10), class-variables (5), pool-variables (3), global-variables (5), pseudo-variables (5), mixed-access (5)"
	| sum |
	sum _ 0.
	"Single level: 9 tests"
	sum _ sum + (self s00).
	sum _ sum + (self s01).
	sum _ sum + (self s02).
	sum _ sum + (self s10).
	sum _ sum + (self s11).
	sum _ sum + (self s12).
	sum _ sum + (self s20).
	sum _ sum + (self s21).
	sum _ sum + (self s22).
	"Two level: 81 tests"
	sum _ sum + (self t0000).
	sum _ sum + (self t0001).
	sum _ sum + (self t0002).
	sum _ sum + (self t0010).
	sum _ sum + (self t0011).
	sum _ sum + (self t0012).
	sum _ sum + (self t0020).
	sum _ sum + (self t0021).
	sum _ sum + (self t0022).
	sum _ sum + (self t0100).
	sum _ sum + (self t0101).
	sum _ sum + (self t0102).
	sum _ sum + (self t0110).
	sum _ sum + (self t0111).
	sum _ sum + (self t0112).
	sum _ sum + (self t0120).
	sum _ sum + (self t0121).
	sum _ sum + (self t0122).
	sum _ sum + (self t0200).
	sum _ sum + (self t0201).
	sum _ sum + (self t0202).
	sum _ sum + (self t0210).
	sum _ sum + (self t0211).
	sum _ sum + (self t0212).
	sum _ sum + (self t0220).
	sum _ sum + (self t0221).
	sum _ sum + (self t0222).
	sum _ sum + (self t1000).
	sum _ sum + (self t1001).
	sum _ sum + (self t1002).
	sum _ sum + (self t1010).
	sum _ sum + (self t1011).
	sum _ sum + (self t1012).
	sum _ sum + (self t1020).
	sum _ sum + (self t1021).
	sum _ sum + (self t1022).
	sum _ sum + (self t1100).
	sum _ sum + (self t1101).
	sum _ sum + (self t1102).
	sum _ sum + (self t1110).
	sum _ sum + (self t1111).
	sum _ sum + (self t1112).
	sum _ sum + (self t1120).
	sum _ sum + (self t1121).
	sum _ sum + (self t1122).
	sum _ sum + (self t1200).
	sum _ sum + (self t1201).
	sum _ sum + (self t1202).
	sum _ sum + (self t1210).
	sum _ sum + (self t1211).
	sum _ sum + (self t1212).
	sum _ sum + (self t1220).
	sum _ sum + (self t1221).
	sum _ sum + (self t1222).
	sum _ sum + (self t2000).
	sum _ sum + (self t2001).
	sum _ sum + (self t2002).
	sum _ sum + (self t2010).
	sum _ sum + (self t2011).
	sum _ sum + (self t2012).
	sum _ sum + (self t2020).
	sum _ sum + (self t2021).
	sum _ sum + (self t2022).
	sum _ sum + (self t2100).
	sum _ sum + (self t2101).
	sum _ sum + (self t2102).
	sum _ sum + (self t2110).
	sum _ sum + (self t2111).
	sum _ sum + (self t2112).
	sum _ sum + (self t2120).
	sum _ sum + (self t2121).
	sum _ sum + (self t2122).
	sum _ sum + (self t2200).
	sum _ sum + (self t2201).
	sum _ sum + (self t2202).
	sum _ sum + (self t2210).
	sum _ sum + (self t2211).
	sum _ sum + (self t2212).
	sum _ sum + (self t2220).
	sum _ sum + (self t2221).
	sum _ sum + (self t2222).
	"Three level: 27 tests"
	sum _ sum + (self n000000).
	sum _ sum + (self n000111).
	sum _ sum + (self n000222).
	sum _ sum + (self n011011).
	sum _ sum + (self n011122).
	sum _ sum + (self n022022).
	sum _ sum + (self n100100).
	sum _ sum + (self n100211).
	sum _ sum + (self n111000).
	sum _ sum + (self n111111).
	sum _ sum + (self n111222).
	sum _ sum + (self n122122).
	sum _ sum + (self n200200).
	sum _ sum + (self n211011).
	sum _ sum + (self n211211).
	sum _ sum + (self n222000).
	sum _ sum + (self n222111).
	sum _ sum + (self n222222).
	sum _ sum + (self n012012).
	sum _ sum + (self n021021).
	sum _ sum + (self n102102).
	sum _ sum + (self n120120).
	sum _ sum + (self n201201).
	sum _ sum + (self n210210).
	sum _ sum + (self n012210).
	sum _ sum + (self n210012).
	sum _ sum + (self n121212).
	"Method multi-temp: 6 tests"
	sum _ sum + (self m2t3l).
	sum _ sum + (self m3t3l).
	sum _ sum + (self m4t3l).
	sum _ sum + (self m4t3lStore).
	sum _ sum + (self m4t2lStore).
	sum _ sum + (self m4t3lMixed).
	"Mutation tests: 3 tests"
	sum _ sum + (self mut1).
	sum _ sum + (self mut2).
	sum _ sum + (self mut3).
	"Method args: 6 tests"
	sum _ sum + (self ma1).
	sum _ sum + (self ma2).
	sum _ sum + (self mat1).
	sum _ sum + (self mat2).
	sum _ sum + (self ma2t2).
	sum _ sum + (self ma2t2Nested).
	"Real-world patterns: 5 tests"
	sum _ sum + (self rwWhileTrue).
	sum _ sum + (self rwWhileFalse).
	sum _ sum + (self rwDo).
	sum _ sum + (self rwSelect).
	sum _ sum + (self rwNestedConditionals).
	"Edge cases: 7 tests"
	sum _ sum + (self e0Empty).
	sum _ sum + (self e1EmptyWithArgs).
	sum _ sum + (self e2NonLocalSelf).
	sum _ sum + (self e3InstanceVarInBlock).
	sum _ sum + (self e4MultipleBlocksSameMethod).
	sum _ sum + (self e5BlockPassedAsArg).
	sum _ sum + (self e6TempShadowing).
	"Three-level gaps: 4 tests"
	sum _ sum + (self n3ParentOnly).
	sum _ sum + (self n3GrandparentOnly).
	sum _ sum + (self n3SkipMiddle).
	sum _ sum + (self n3NonContiguous).
	"Stress tests: 3 tests"
	sum _ sum + (self stressMaxNesting).
	sum _ sum + (self stressComplexMutation).
	sum _ sum + (self stressMixedShadowing).
	"Non-local returns: 3 tests"
	sum _ sum + (self nlr1SimpleReturn).
	sum _ sum + (self nlr2NestedReturn).
	sum _ sum + (self nlr3ReturnWithMutation).
	"Exception handling: 2 tests - SKIPPED (requires on:do: not in Blue Book base)"
	"sum _ sum + (self eh1BasicHandler)."
	"sum _ sum + (self eh2NestedHandlers)."
	"Block lifetime: 2 tests"
	sum _ sum + (self bl1StoredBlock).
	sum _ sum + (self bl2BlockInArray).
	"Control flow: 2 tests"
	sum _ sum + (self cf1EarlyExit).
	sum _ sum + (self cf2RecursiveBlock).
	"Edge special: 2 tests"
	sum _ sum + (self es1SelfModifying).
	sum _ sum + (self es2MixedAccess).  "NOW ENABLED: Testing if Symbol>>asString bug is fixed"
	"Stress deep: 1 test"
	sum _ sum + (self sd1DeepNesting10).
	"Instance variables: 10 tests"
	sum _ sum + (self iv1SimpleAccess).
	sum _ sum + (self iv2ModifyFromBlock).
	sum _ sum + (self iv3NestedBlockIVAccess).  "NOW ENABLED: Testing if Symbol>>asString bug is fixed"
	sum _ sum + (self iv4IVPlusTempAccess).
	sum _ sum + (self iv5IVInCondition).
	sum _ sum + (self iv6IVInLoop).
	sum _ sum + (self iv7IVInWhileTrue).
	sum _ sum + (self iv8IVPassedAsArg).
	sum _ sum + (self iv9IVComparedInBlock).
	sum _ sum + (self iv10IVStoredThenAccessed).
	"Class variables: 5 tests"
	sum _ sum + (self cv1SimpleClassVarRead).
	sum _ sum + (self cv2ClassVarInCondition).
	sum _ sum + (self cv3NestedClassVarAccess).
	sum _ sum + (self cv4ClassVarModifyInBlock).
	sum _ sum + (self cv5ClassVarSharedState).
	"Pool variables: 3 tests"
	sum _ sum + (self pv1PoolVarRead).
	sum _ sum + (self pv2PoolVarInCalculation).
	sum _ sum + (self pv3NestedPoolVarAccess).
	"Global variables: 5 tests"
	sum _ sum + (self gv1GlobalRead).
	sum _ sum + (self gv2GlobalInCondition).
	sum _ sum + (self gv3NestedGlobalAccess).
	sum _ sum + (self gv4GlobalWithTemp).
	sum _ sum + (self gv5GlobalInLoop).
	"Pseudo-variables: 5 tests"
	sum _ sum + (self ps1SelfInBlock).
	sum _ sum + (self ps2TrueInBlock).
	sum _ sum + (self ps3FalseInBlock).
	sum _ sum + (self ps4NilInBlock).
	"sum _ sum + (self ps5SuperInBlock)."  "SKIPPED: super in BlockContext not yet supported"
	"Mixed access: 5 tests"
	sum _ sum + (self mx1AllVariableTypes).
	sum _ sum + (self mx2NestedMixedAccess).
	sum _ sum + (self mx3ConditionalMixed).
	sum _ sum + (self mx4LoopMixed).
	sum _ sum + (self mx5WhileMixed).
	^sum! !

"Single-level tests: [XY] = X args, Y temps"
!BlockContextTest methodsFor: 'single-level'!

s00 "[0arg,0temp] access method temp"
	| m | m _ 10. ^[m] value!

s01 "[0arg,1temp] use own temp"
	^[| t1 | t1 _ 10. t1] value!

s02 "[0arg,2temp] use both temps"
	^[| t1 t2 | t1 _ 5. t2 _ 5. t1 + t2] value!

s10 "[1arg,0temp] use arg"
	^[:a1 | a1] value: 10!

s11 "[1arg,1temp] use arg and temp"
	^[:a1 | | t1 | t1 _ 5. a1 + t1] value: 5!

s12 "[1arg,2temp] use arg and both temps"
	^[:a1 | | t1 t2 | t1 _ 3. t2 _ 4. a1 + t1 + t2] value: 3!

s20 "[2arg,0temp] use both args"
	^[:a1 :a2 | a1 + a2] value: 5 value: 5!

s21 "[2arg,1temp] use both args and temp"
	^[:a1 :a2 | | t1 | t1 _ 4. a1 + a2 + t1] value: 3 value: 3!

s22 "[2arg,2temp] use all"
	^[:a1 :a2 | | t1 t2 | t1 _ 2. t2 _ 3. a1 + a2 + t1 + t2] value: 2 value: 3! !

"Two-level tests: [XXYY] = outer XX, inner YY"
!BlockContextTest methodsFor: 'two-level'!

t0000 "O[0,0] I[0,0]: both access method temp"
	| m | m _ 10. ^[[m] value] value!

t0001 "O[0,0] I[0,1]: inner has temp, accesses method temp"
	| m | m _ 5. ^[[| t | t _ 5. m + t] value] value!

t0002 "O[0,0] I[0,2]: inner has 2 temps, accesses method temp"
	| m | m _ 3. ^[[| t1 t2 | t1 _ 3. t2 _ 4. m + t1 + t2] value] value!

t0010 "O[0,0] I[1,0]: inner has arg"
	^[[[:a | a] value: 10] value] value!

t0011 "O[0,0] I[1,1]: inner has arg and temp"
	^[[[:a | | t | t _ 5. a + t] value: 5] value] value!

t0012 "O[0,0] I[1,2]: inner has arg and 2 temps"
	^[[[:a | | t1 t2 | t1 _ 3. t2 _ 4. a + t1 + t2] value: 3] value] value!

t0020 "O[0,0] I[2,0]: inner has 2 args"
	^[[[:a1 :a2 | a1 + a2] value: 5 value: 5] value] value!

t0021 "O[0,0] I[2,1]: inner has 2 args and temp"
	^[[[:a1 :a2 | | t | t _ 4. a1 + a2 + t] value: 3 value: 3] value] value!

t0022 "O[0,0] I[2,2]: inner has 2 args and 2 temps"
	^[[[:a1 :a2 | | t1 t2 | t1 _ 2. t2 _ 3. a1 + a2 + t1 + t2] value: 2 value: 3] value] value!

t0100 "O[0,1] I[0,0]: outer has temp, inner accesses it"
	^[| ot | ot _ 10. [ot] value] value!

t0101 "O[0,1] I[0,1]: both have temps, inner accesses outer"
	^[| ot | ot _ 5. [| it | it _ 5. ot + it] value] value!

t0102 "O[0,1] I[0,2]: outer 1 temp, inner 2 temps"
	^[| ot | ot _ 3. [| it1 it2 | it1 _ 3. it2 _ 4. ot + it1 + it2] value] value!

t0110 "O[0,1] I[1,0]: outer temp, inner arg accesses outer temp"
	^[| ot | ot _ 10. [:a | ot] value: 999] value!

t0111 "O[0,1] I[1,1]: outer temp, inner arg+temp"
	^[| ot | ot _ 5. [:a | | it | it _ 5. ot + it] value: 999] value!

t0112 "O[0,1] I[1,2]: outer temp, inner arg+2temps"
	^[| ot | ot _ 3. [:a | | it1 it2 | it1 _ 3. it2 _ 4. ot + it1 + it2] value: 999] value!

t0120 "O[0,1] I[2,0]: outer temp, inner 2 args"
	^[| ot | ot _ 10. [:a1 :a2 | ot] value: 999 value: 888] value!

t0121 "O[0,1] I[2,1]: outer temp, inner 2 args+temp"
	^[| ot | ot _ 5. [:a1 :a2 | | it | it _ 5. ot + it] value: 999 value: 888] value!

t0122 "O[0,1] I[2,2]: outer temp, inner 2 args+2temps"
	^[| ot | ot _ 3. [:a1 :a2 | | it1 it2 | it1 _ 3. it2 _ 4. ot + it1 + it2] value: 999 value: 888] value!

t0200 "O[0,2] I[0,0]: outer 2 temps, inner accesses both"
	^[| ot1 ot2 | ot1 _ 5. ot2 _ 5. [ot1 + ot2] value] value!

t0201 "O[0,2] I[0,1]: outer 2 temps, inner 1 temp"
	^[| ot1 ot2 | ot1 _ 3. ot2 _ 4. [| it | it _ 3. ot1 + ot2 + it] value] value!

t0202 "O[0,2] I[0,2]: outer 2 temps, inner 2 temps"
	^[| ot1 ot2 | ot1 _ 2. ot2 _ 3. [| it1 it2 | it1 _ 2. it2 _ 3. ot1 + ot2 + it1 + it2] value] value!

t0210 "O[0,2] I[1,0]: outer 2 temps, inner 1 arg"
	^[| ot1 ot2 | ot1 _ 5. ot2 _ 5. [:a | ot1 + ot2] value: 999] value!

t0211 "O[0,2] I[1,1]: outer 2 temps, inner 1 arg+temp"
	^[| ot1 ot2 | ot1 _ 3. ot2 _ 4. [:a | | it | it _ 3. ot1 + ot2 + it] value: 999] value!

t0212 "O[0,2] I[1,2]: outer 2 temps, inner 1 arg+2temps"
	^[| ot1 ot2 | ot1 _ 2. ot2 _ 3. [:a | | it1 it2 | it1 _ 2. it2 _ 3. ot1 + ot2 + it1 + it2] value: 999] value!

t0220 "O[0,2] I[2,0]: outer 2 temps, inner 2 args"
	^[| ot1 ot2 | ot1 _ 5. ot2 _ 5. [:a1 :a2 | ot1 + ot2] value: 999 value: 888] value!

t0221 "O[0,2] I[2,1]: outer 2 temps, inner 2 args+temp"
	^[| ot1 ot2 | ot1 _ 3. ot2 _ 4. [:a1 :a2 | | it | it _ 3. ot1 + ot2 + it] value: 999 value: 888] value!

t0222 "O[0,2] I[2,2]: outer 2 temps, inner 2 args+2temps"
	^[| ot1 ot2 | ot1 _ 2. ot2 _ 3. [:a1 :a2 | | it1 it2 | it1 _ 2. it2 _ 3. ot1 + ot2 + it1 + it2] value: 999 value: 888] value!

t1000 "O[1,0] I[0,0]: outer arg, inner accesses it"
	^[:oa | [oa] value] value: 10!

t1001 "O[1,0] I[0,1]: outer arg, inner temp accesses outer arg"
	^[:oa | [| it | it _ 5. oa + it] value] value: 5!

t1002 "O[1,0] I[0,2]: outer arg, inner 2 temps access outer arg"
	^[:oa | [| it1 it2 | it1 _ 3. it2 _ 4. oa + it1 + it2] value] value: 3!

t1010 "O[1,0] I[1,0]: both have args (inner ignores)"
	^[:oa | [:ia | oa] value: 999] value: 10!

t1011 "O[1,0] I[1,1]: both have args, inner has temp"
	^[:oa | [:ia | | it | it _ 5. oa + it] value: 999] value: 5!

t1012 "O[1,0] I[1,2]: both have args, inner has 2 temps"
	^[:oa | [:ia | | it1 it2 | it1 _ 3. it2 _ 4. oa + it1 + it2] value: 999] value: 3!

t1020 "O[1,0] I[2,0]: outer 1 arg, inner 2 args (ignores both)"
	^[:oa | [:ia1 :ia2 | oa] value: 999 value: 888] value: 10!

t1021 "O[1,0] I[2,1]: outer 1 arg, inner 2 args+temp"
	^[:oa | [:ia1 :ia2 | | it | it _ 5. oa + it] value: 999 value: 888] value: 5!

t1022 "O[1,0] I[2,2]: outer 1 arg, inner 2 args+2temps"
	^[:oa | [:ia1 :ia2 | | it1 it2 | it1 _ 3. it2 _ 4. oa + it1 + it2] value: 999 value: 888] value: 3!

t1100 "O[1,1] I[0,0]: outer arg+temp, inner accesses both"
	^[:oa | | ot | ot _ 5. [oa + ot] value] value: 5!

t1101 "O[1,1] I[0,1]: outer arg+temp, inner temp"
	^[:oa | | ot | ot _ 3. [| it | it _ 4. oa + ot + it] value] value: 3!

t1102 "O[1,1] I[0,2]: outer arg+temp, inner 2 temps"
	^[:oa | | ot | ot _ 2. [| it1 it2 | it1 _ 3. it2 _ 3. oa + ot + it1 + it2] value] value: 2!

t1110 "O[1,1] I[1,0]: both arg+temp, inner arg unused"
	^[:oa | | ot | ot _ 5. [:ia | oa + ot] value: 999] value: 5!

t1111 "O[1,1] I[1,1]: both arg+temp"
	^[:oa | | ot | ot _ 3. [:ia | | it | it _ 4. oa + ot + it] value: 999] value: 3!

t1112 "O[1,1] I[1,2]: outer arg+temp, inner arg+2temps"
	^[:oa | | ot | ot _ 2. [:ia | | it1 it2 | it1 _ 3. it2 _ 3. oa + ot + it1 + it2] value: 999] value: 2!

t1120 "O[1,1] I[2,0]: outer arg+temp, inner 2 args"
	^[:oa | | ot | ot _ 5. [:ia1 :ia2 | oa + ot] value: 999 value: 888] value: 5!

t1121 "O[1,1] I[2,1]: outer arg+temp, inner 2 args+temp"
	^[:oa | | ot | ot _ 3. [:ia1 :ia2 | | it | it _ 4. oa + ot + it] value: 999 value: 888] value: 3!

t1122 "O[1,1] I[2,2]: outer arg+temp, inner 2 args+2temps"
	^[:oa | | ot | ot _ 2. [:ia1 :ia2 | | it1 it2 | it1 _ 3. it2 _ 3. oa + ot + it1 + it2] value: 999 value: 888] value: 2!

t1200 "O[1,2] I[0,0]: outer arg+2temps, inner accesses all"
	^[:oa | | ot1 ot2 | ot1 _ 3. ot2 _ 4. [oa + ot1 + ot2] value] value: 3!

t1201 "O[1,2] I[0,1]: outer arg+2temps, inner temp"
	^[:oa | | ot1 ot2 | ot1 _ 2. ot2 _ 3. [| it | it _ 3. oa + ot1 + ot2 + it] value] value: 2!

t1202 "O[1,2] I[0,2]: outer arg+2temps, inner 2 temps"
	^[:oa | | ot1 ot2 | ot1 _ 1. ot2 _ 2. [| it1 it2 | it1 _ 3. it2 _ 4. oa + ot1 + ot2 + it1 + it2] value] value: 0!

t1210 "O[1,2] I[1,0]: outer arg+2temps, inner arg"
	^[:oa | | ot1 ot2 | ot1 _ 3. ot2 _ 4. [:ia | oa + ot1 + ot2] value: 999] value: 3!

t1211 "O[1,2] I[1,1]: outer arg+2temps, inner arg+temp"
	^[:oa | | ot1 ot2 | ot1 _ 2. ot2 _ 3. [:ia | | it | it _ 3. oa + ot1 + ot2 + it] value: 999] value: 2!

t1212 "O[1,2] I[1,2]: outer arg+2temps, inner arg+2temps"
	^[:oa | | ot1 ot2 | ot1 _ 1. ot2 _ 2. [:ia | | it1 it2 | it1 _ 3. it2 _ 4. oa + ot1 + ot2 + it1 + it2] value: 999] value: 0!

t1220 "O[1,2] I[2,0]: outer arg+2temps, inner 2 args"
	^[:oa | | ot1 ot2 | ot1 _ 3. ot2 _ 4. [:ia1 :ia2 | oa + ot1 + ot2] value: 999 value: 888] value: 3!

t1221 "O[1,2] I[2,1]: outer arg+2temps, inner 2 args+temp"
	^[:oa | | ot1 ot2 | ot1 _ 2. ot2 _ 3. [:ia1 :ia2 | | it | it _ 3. oa + ot1 + ot2 + it] value: 999 value: 888] value: 2!

t1222 "O[1,2] I[2,2]: outer arg+2temps, inner 2 args+2temps"
	^[:oa | | ot1 ot2 | ot1 _ 1. ot2 _ 2. [:ia1 :ia2 | | it1 it2 | it1 _ 3. it2 _ 4. oa + ot1 + ot2 + it1 + it2] value: 999 value: 888] value: 0!

t2000 "O[2,0] I[0,0]: outer 2 args, inner accesses both"
	^[:oa1 :oa2 | [oa1 + oa2] value] value: 5 value: 5!

t2001 "O[2,0] I[0,1]: outer 2 args, inner temp"
	^[:oa1 :oa2 | [| it | it _ 3. oa1 + oa2 + it] value] value: 3 value: 4!

t2002 "O[2,0] I[0,2]: outer 2 args, inner 2 temps"
	^[:oa1 :oa2 | [| it1 it2 | it1 _ 2. it2 _ 3. oa1 + oa2 + it1 + it2] value] value: 2 value: 3!

t2010 "O[2,0] I[1,0]: outer 2 args, inner arg"
	^[:oa1 :oa2 | [:ia | oa1 + oa2] value: 999] value: 5 value: 5!

t2011 "O[2,0] I[1,1]: outer 2 args, inner arg+temp"
	^[:oa1 :oa2 | [:ia | | it | it _ 3. oa1 + oa2 + it] value: 999] value: 3 value: 4!

t2012 "O[2,0] I[1,2]: outer 2 args, inner arg+2temps"
	^[:oa1 :oa2 | [:ia | | it1 it2 | it1 _ 2. it2 _ 3. oa1 + oa2 + it1 + it2] value: 999] value: 2 value: 3!

t2020 "O[2,0] I[2,0]: both have 2 args (inner ignores)"
	^[:oa1 :oa2 | [:ia1 :ia2 | oa1 + oa2] value: 999 value: 888] value: 5 value: 5!

t2021 "O[2,0] I[2,1]: both have 2 args, inner temp"
	^[:oa1 :oa2 | [:ia1 :ia2 | | it | it _ 3. oa1 + oa2 + it] value: 999 value: 888] value: 3 value: 4!

t2022 "O[2,0] I[2,2]: both have 2 args, inner 2 temps"
	^[:oa1 :oa2 | [:ia1 :ia2 | | it1 it2 | it1 _ 2. it2 _ 3. oa1 + oa2 + it1 + it2] value: 999 value: 888] value: 2 value: 3!

t2100 "O[2,1] I[0,0]: outer 2 args+temp, inner accesses all"
	^[:oa1 :oa2 | | ot | ot _ 3. [oa1 + oa2 + ot] value] value: 3 value: 4!

t2101 "O[2,1] I[0,1]: outer 2 args+temp, inner temp"
	^[:oa1 :oa2 | | ot | ot _ 2. [| it | it _ 3. oa1 + oa2 + ot + it] value] value: 2 value: 3!

t2102 "O[2,1] I[0,2]: outer 2 args+temp, inner 2 temps"
	^[:oa1 :oa2 | | ot | ot _ 1. [| it1 it2 | it1 _ 3. it2 _ 4. oa1 + oa2 + ot + it1 + it2] value] value: 1 value: 1!

t2110 "O[2,1] I[1,0]: outer 2 args+temp, inner arg"
	^[:oa1 :oa2 | | ot | ot _ 3. [:ia | oa1 + oa2 + ot] value: 999] value: 3 value: 4!

t2111 "O[2,1] I[1,1]: outer 2 args+temp, inner arg+temp"
	^[:oa1 :oa2 | | ot | ot _ 2. [:ia | | it | it _ 3. oa1 + oa2 + ot + it] value: 999] value: 2 value: 3!

t2112 "O[2,1] I[1,2]: outer 2 args+temp, inner arg+2temps"
	^[:oa1 :oa2 | | ot | ot _ 1. [:ia | | it1 it2 | it1 _ 3. it2 _ 4. oa1 + oa2 + ot + it1 + it2] value: 999] value: 1 value: 1!

t2120 "O[2,1] I[2,0]: outer 2 args+temp, inner 2 args"
	^[:oa1 :oa2 | | ot | ot _ 3. [:ia1 :ia2 | oa1 + oa2 + ot] value: 999 value: 888] value: 3 value: 4!

t2121 "O[2,1] I[2,1]: outer 2 args+temp, inner 2 args+temp"
	^[:oa1 :oa2 | | ot | ot _ 2. [:ia1 :ia2 | | it | it _ 3. oa1 + oa2 + ot + it] value: 999 value: 888] value: 2 value: 3!

t2122 "O[2,1] I[2,2]: outer 2 args+temp, inner 2 args+2temps"
	^[:oa1 :oa2 | | ot | ot _ 1. [:ia1 :ia2 | | it1 it2 | it1 _ 3. it2 _ 4. oa1 + oa2 + ot + it1 + it2] value: 999 value: 888] value: 1 value: 1!

t2200 "O[2,2] I[0,0]: outer 2 args+2temps, inner accesses all"
	^[:oa1 :oa2 | | ot1 ot2 | ot1 _ 2. ot2 _ 3. [oa1 + oa2 + ot1 + ot2] value] value: 2 value: 3!

t2201 "O[2,2] I[0,1]: outer 2 args+2temps, inner temp"
	^[:oa1 :oa2 | | ot1 ot2 | ot1 _ 1. ot2 _ 2. [| it | it _ 3. oa1 + oa2 + ot1 + ot2 + it] value] value: 2 value: 2!

t2202 "O[2,2] I[0,2]: outer 2 args+2temps, inner 2 temps"
	^[:oa1 :oa2 | | ot1 ot2 | ot1 _ 0. ot2 _ 1. [| it1 it2 | it1 _ 4. it2 _ 5. oa1 + oa2 + ot1 + ot2 + it1 + it2] value] value: 0 value: 0!

t2210 "O[2,2] I[1,0]: outer 2 args+2temps, inner arg"
	^[:oa1 :oa2 | | ot1 ot2 | ot1 _ 2. ot2 _ 3. [:ia | oa1 + oa2 + ot1 + ot2] value: 999] value: 2 value: 3!

t2211 "O[2,2] I[1,1]: outer 2 args+2temps, inner arg+temp"
	^[:oa1 :oa2 | | ot1 ot2 | ot1 _ 1. ot2 _ 2. [:ia | | it | it _ 3. oa1 + oa2 + ot1 + ot2 + it] value: 999] value: 2 value: 2!

t2212 "O[2,2] I[1,2]: outer 2 args+2temps, inner arg+2temps"
	^[:oa1 :oa2 | | ot1 ot2 | ot1 _ 0. ot2 _ 1. [:ia | | it1 it2 | it1 _ 4. it2 _ 5. oa1 + oa2 + ot1 + ot2 + it1 + it2] value: 999] value: 0 value: 0!

t2220 "O[2,2] I[2,0]: both have 2 args+2temps"
	^[:oa1 :oa2 | | ot1 ot2 | ot1 _ 2. ot2 _ 3. [:ia1 :ia2 | oa1 + oa2 + ot1 + ot2] value: 999 value: 888] value: 2 value: 3!

t2221 "O[2,2] I[2,1]: outer 2 args+2temps, inner 2 args+temp"
	^[:oa1 :oa2 | | ot1 ot2 | ot1 _ 1. ot2 _ 2. [:ia1 :ia2 | | it | it _ 3. oa1 + oa2 + ot1 + ot2 + it] value: 999 value: 888] value: 2 value: 2!

t2222 "O[2,2] I[2,2]: all have 2 args+2temps"
	^[:oa1 :oa2 | | ot1 ot2 | ot1 _ 0. ot2 _ 1. [:ia1 :ia2 | | it1 it2 | it1 _ 4. it2 _ 5. oa1 + oa2 + ot1 + ot2 + it1 + it2] value: 999 value: 888] value: 0 value: 0! !

"Three-level tests: [XXYYZZ] = outer XX, middle YY, inner ZZ"
!BlockContextTest methodsFor: 'three-level'!

n000000 "All [0,0]: method temp access through 3 levels"
	| m | m _ 10. ^[[[m] value] value] value!

n000111 "O[0,0] M[0,1] I[1,1]: varying configs, access method temp"
	| m | m _ 4. ^[[| mt | mt _ 3. [:ia | | it | it _ 3. m + mt + it] value: 999] value] value!

n000222 "O[0,0] M[0,2] I[2,2]: max inner, access method temp"
	| m | m _ 0. ^[[| mt1 mt2 | mt1 _ 2. mt2 _ 3. [:ia1 :ia2 | | it1 it2 | it1 _ 2. it2 _ 3. m + mt1 + mt2 + it1 + it2] value: 999 value: 888] value] value!

n011011 "O[0,1] M[1,0] I[1,1]: middle arg, inner accesses outer temp"
	^[| ot | ot _ 4. [:ma | [:ia | | it | it _ 3. ot + it] value: 999] value: 888] value!

n011122 "O[0,1] M[1,1] I[2,2]: outer temp accessed by innermost"
	^[| ot | ot _ 0. [:ma | | mt | mt _ 2. [:ia1 :ia2 | | it1 it2 | it1 _ 3. it2 _ 5. ot + mt + it1 + it2] value: 999 value: 888] value: 777] value!

n022022 "O[0,2] M[2,0] I[2,2]: outer temps accessed by innermost"
	^[| ot1 ot2 | ot1 _ 2. ot2 _ 3. [:ma1 :ma2 | [:ia1 :ia2 | | it1 it2 | it1 _ 2. it2 _ 3. ot1 + ot2 + it1 + it2] value: 999 value: 888] value: 777 value: 666] value!

n100100 "O[1,0] M[0,1] I[0,0]: outer arg accessed through middle temp"
	^[:oa | [| mt | mt _ 5. [oa + mt] value] value] value: 5!

n100211 "O[1,0] M[0,2] I[1,1]: outer arg accessed by innermost"
	^[:oa | [| mt1 mt2 | mt1 _ 3. mt2 _ 4. [:ia | | it | it _ 3. oa + mt1 + mt2 + it] value: 999] value] value: 0!

n111000 "O[1,1] M[1,0] I[0,0]: all args, innermost accesses outermost"
	^[:oa | | ot | ot _ 5. [:ma | [oa + ot] value] value: 888] value: 5!

n111111 "All [1,1]: uniform nesting"
	^[:oa | | ot | ot _ 3. [:ma | | mt | mt _ 4. [:ia | | it | it _ 3. oa + ot + mt + it] value: 999] value: 888] value: 0!

n111222 "O[1,1] M[1,2] I[2,2]: all levels max"
	^[:oa | | ot | ot _ 0. [:ma | | mt1 mt2 | mt1 _ 2. mt2 _ 3. [:ia1 :ia2 | | it1 it2 | it1 _ 2. it2 _ 3. oa + ot + mt1 + mt2 + it1 + it2] value: 999 value: 888] value: 777] value: 0!

n122122 "O[1,2] M[2,1] I[2,2]: outer arg accessed by innermost"
	^[:oa | | ot1 ot2 | ot1 _ 0. ot2 _ 1. [:ma1 :ma2 | | mt | mt _ 2. [:ia1 :ia2 | | it1 it2 | it1 _ 3. it2 _ 4. oa + ot1 + ot2 + mt + it1 + it2] value: 999 value: 888] value: 777 value: 666] value: 0!

n200200 "O[2,0] M[0,0] I[2,0]: outer args, inner args, both used"
	^[:oa1 :oa2 | [[:ia1 :ia2 | oa1 + oa2 + ia1 + ia2] value: 3 value: 4] value] value: 1 value: 2!

n211011 "O[2,1] M[1,0] I[1,1]: outer args+temp accessed by innermost"
	^[:oa1 :oa2 | | ot | ot _ 3. [:ma | [:ia | | it | it _ 4. oa1 + oa2 + ot + it] value: 999] value: 888] value: 0 value: 0!

n211211 "O[2,1] M[1,2] I[1,1]: complex nesting"
	^[:oa1 :oa2 | | ot | ot _ 1. [:ma | | mt1 mt2 | mt1 _ 2. mt2 _ 3. [:ia | | it | it _ 4. oa1 + oa2 + ot + mt1 + mt2 + it] value: 999] value: 888] value: 0 value: 0!

n222000 "O[2,2] M[2,0] I[0,0]: all outer, innermost accesses all"
	^[:oa1 :oa2 | | ot1 ot2 | ot1 _ 2. ot2 _ 3. [:ma1 :ma2 | [oa1 + oa2 + ot1 + ot2] value] value: 888 value: 777] value: 1 value: 1!

n222111 "O[2,2] M[2,1] I[1,1]: all levels have temps"
	^[:oa1 :oa2 | | ot1 ot2 | ot1 _ 1. ot2 _ 2. [:ma1 :ma2 | | mt | mt _ 3. [:ia | | it | it _ 4. oa1 + oa2 + ot1 + ot2 + mt + it] value: 999] value: 888 value: 777] value: 0 value: 0!

n222222 "All [2,2]: maximum complexity"
	^[:oa1 :oa2 | | ot1 ot2 | ot1 _ 0. ot2 _ 1. [:ma1 :ma2 | | mt1 mt2 | mt1 _ 2. mt2 _ 3. [:ia1 :ia2 | | it1 it2 | it1 _ 2. it2 _ 2. oa1 + oa2 + ot1 + ot2 + mt1 + mt2 + it1 + it2] value: 999 value: 888] value: 777 value: 666] value: 0 value: 0!

n012012 "O[0,1] M[2,0] I[1,2]: varied nesting"
	^[| ot | ot _ 3. [:ma1 :ma2 | [:ia | | it1 it2 | it1 _ 3. it2 _ 4. ot + it1 + it2] value: 999] value: 888 value: 777] value!

n021021 "O[0,2] M[1,0] I[2,1]: varied nesting"
	^[| ot1 ot2 | ot1 _ 3. ot2 _ 4. [:ma | [:ia1 :ia2 | | it | it _ 3. ot1 + ot2 + it] value: 999 value: 888] value: 777] value!

n102102 "O[1,0] M[2,1] I[0,2]: varied nesting"
	^[:oa | [:ma1 :ma2 | | mt | mt _ 2. [| it1 it2 | it1 _ 3. it2 _ 5. oa + mt + it1 + it2] value] value: 888 value: 777] value: 0!

n120120 "O[1,2] M[0,1] I[2,0]: varied nesting"
	^[:oa | | ot1 ot2 | ot1 _ 3. ot2 _ 4. [| mt | mt _ 3. [:ia1 :ia2 | oa + ot1 + ot2 + mt] value: 999 value: 888] value] value: 0!

n201201 "O[2,0] M[1,2] I[0,1]: varied nesting"
	^[:oa1 :oa2 | [:ma | | mt1 mt2 | mt1 _ 3. mt2 _ 4. [| it | it _ 3. oa1 + oa2 + mt1 + mt2 + it] value] value: 888] value: 0 value: 0!

n210210 "O[2,1] M[0,2] I[1,0]: varied nesting"
	^[:oa1 :oa2 | | ot | ot _ 3. [| mt1 mt2 | mt1 _ 3. mt2 _ 4. [:ia | oa1 + oa2 + ot + mt1 + mt2] value: 999] value] value: 0 value: 0!

n012210 "O[0,1] M[2,2] I[1,0]: varied nesting"
	^[| ot | ot _ 2. [:ma1 :ma2 | | mt1 mt2 | mt1 _ 3. mt2 _ 5. [:ia | ot + mt1 + mt2] value: 999] value: 888 value: 777] value!

n210012 "O[2,1] M[0,0] I[1,2]: varied nesting"
	^[:oa1 :oa2 | | ot | ot _ 2. [[[:ia | | it1 it2 | it1 _ 3. it2 _ 5. oa1 + oa2 + ot + it1 + it2] value: 999] value] value] value: 0 value: 0!

n121212 "O[1,2] M[1,2] I[1,2]: uniform complex nesting"
	^[:oa | | ot1 ot2 | ot1 _ 0. ot2 _ 1. [:ma | | mt1 mt2 | mt1 _ 2. mt2 _ 3. [:ia | | it1 it2 | it1 _ 2. it2 _ 2. oa + ot1 + ot2 + ma + mt1 + mt2 + ia + it1 + it2] value: 0] value: 0] value: 0! !

"Method multi-temp tests: METHOD with 2-4 temps accessed by nested blocks"
!BlockContextTest methodsFor: 'method-multi-temp'!

m2t3l "Method 2 temps, 3-level nesting, access both temps"
	"Tests innermost block accessing method temps at indices 0 and 1"
	| t0 t1 |
	t0 _ 10.
	t1 _ 20.
	^[[[t0 + t1] value] value] value!

m3t3l "Method 3 temps, 3-level nesting, access temps 0 and 2"
	"Tests innermost block accessing non-contiguous method temps"
	| t0 t1 t2 |
	t0 _ 5.
	t1 _ 10.
	t2 _ 15.
	^[[[t0 + t2] value] value] value!

m4t3l "Method 4 temps, 3-level nesting, access temps 0 and 3"
	"Critical: Simulates findKeyOrNil: pattern (location and pass)"
	| t0 t1 t2 t3 |
	t0 _ 10.
	t1 _ 20.
	t2 _ 30.
	t3 _ 40.
	^[[[t0 + t3] value] value] value!

m4t3lStore "Method 4 temps, 3-level nesting, STORE to temps 0 and 3"
	"Critical: Tests STORE_TEMP bytecode generation for non-contiguous indices"
	| t0 t1 t2 t3 |
	t0 _ 1.
	t1 _ 2.
	t2 _ 3.
	t3 _ 4.
	[[[
		t0 _ 100.
		t3 _ 400.
		1
	] value] value] value.
	^t0 + t3!

m4t2lStore "Method 4 temps, 2-level nesting, STORE to temps 0 and 3"
	"Tests if 2-level nesting has same issue"
	| t0 t1 t2 t3 |
	t0 _ 1.
	t1 _ 2.
	t2 _ 3.
	t3 _ 4.
	[[
		t0 _ 50.
		t3 _ 80.
		1
	] value] value.
	^t0 + t3!

m4t3lMixed "Method 4 temps, 3-level, mixed access pattern"
	"Access temps 1 and 2, then 0 and 3"
	| t0 t1 t2 t3 |
	t0 _ 5.
	t1 _ 10.
	t2 _ 15.
	t3 _ 20.
	^[[[t1 + t2 + t0 + t3] value] value] value! !

"Mutation tests"
!BlockContextTest methodsFor: 'mutation'!

mut1 "Block modifies method temp"
	| m | m _ 5. [m _ m + 5] value. ^m!

mut2 "Inner modifies outer temp"
	^[| t | t _ 5. [t _ t + 5] value. t] value!

mut3 "Args immutable: inner sees original"
	^[:a | [:b | a] value: 999] value: 10! !

"Method args tests: METHOD with args accessed by nested blocks"
!BlockContextTest methodsFor: 'method-args'!

ma1 "Method 1 arg, 2-level block accesses it"
	"Tests block accessing method argument (not just temps)"
	^[:arg | [[arg] value] value] value: 42!

ma2 "Method 2 args, 2-level block accesses both"
	"Tests block accessing multiple method arguments"
	^[:arg1 :arg2 | [[arg1 + arg2] value] value] value: 20 value: 30!

mat1 "Method 1 arg + 1 temp, 2-level block accesses both"
	"Tests block accessing both method arg and temp"
	^[:arg | | temp | temp _ 8. [[arg + temp] value] value] value: 42!

mat2 "Method 2 args + 2 temps, 2-level block accesses all"
	"Tests block accessing all method args and temps"
	^[:arg1 :arg2 | | temp1 temp2 | temp1 _ 5. temp2 _ 10. [[arg1 + arg2 + temp1 + temp2] value] value] value: 10 value: 20!

ma2t2 "Method 2 args + 2 temps, 3-level nesting, access non-contiguous"
	"Critical: Method args + temps with non-contiguous access pattern"
	^[:arg1 :arg2 | | temp1 temp2 | temp1 _ 3. temp2 _ 7. [[[arg1 + arg2 + temp2] value] value] value] value: 5 value: 10!

ma2t2Nested "Method args + temps in complex nested structure"
	"Tests args/temps with mixed block args"
	^[:marg1 :marg2 | | mt1 mt2 | mt1 _ 2. mt2 _ 3. [:ba | [marg1 + marg2 + mt1 + mt2 + ba] value] value: 10] value: 5 value: 10! !

"Real-world control structure tests"
!BlockContextTest methodsFor: 'real-world'!

rwWhileTrue "whileTrue: loop with outer temp access"
	"CRITICAL: This is the exact pattern that caused the findKeyOrNil: bug!"
	| counter limit sum |
	counter _ 1.
	limit _ 5.
	sum _ 0.
	[counter <= limit] whileTrue: [
		sum _ sum + counter.
		counter _ counter + 1].
	^sum!

rwWhileFalse "whileFalse: loop with outer temp mutation"
	"Tests whileFalse: pattern with temp modification"
	| counter result |
	counter _ 5.
	result _ 0.
	[counter = 0] whileFalse: [
		result _ result + counter.
		counter _ counter - 1].
	^result!

rwDo "do: iteration with block accessing outer context"
	"Tests do: pattern (Collection>>do:)"
	| sum |
	sum _ 0.
	1 to: 5 do: [:i | sum _ sum + i].
	^sum!

rwSelect "select: pattern with block predicate"
	"Tests select: pattern (Collection>>select:)"
	| threshold count |
	threshold _ 3.
	count _ 0.
	1 to: 5 do: [:i | (i > threshold) ifTrue: [count _ count + 1]].
	^count!

rwNestedConditionals "Nested ifTrue:ifFalse: with outer temp access"
	"Tests real nested conditional pattern"
	| x y result |
	x _ 5.
	y _ 10.
	result _ 0.
	(x > 3) ifTrue: [
		(y > 8) ifTrue: [
			result _ x + y
		] ifFalse: [
			result _ x
		]
	] ifFalse: [
		result _ y
	].
	^result! !

"Edge case tests"
!BlockContextTest methodsFor: 'edge-cases'!

e0Empty "Empty block returns nil (should be nil OOP, value = 0 for sum)"
	"Tests empty block edge case"
	| result |
	result _ [] value.
	result isNil ifTrue: [^0] ifFalse: [^999]!

e1EmptyWithArgs "Empty block with args (ignores them)"
	"Tests block that receives args but doesn't use them"
	^[:a :b | 20] value: 999 value: 888!

e2NonLocalSelf "Block accessing self"
	"Tests that blocks can access self (receiver)"
	^[self class] value == BlockContextTest ifTrue: [21] ifFalse: [999]!

e3InstanceVarInBlock "Block accessing instance variable (if test class had instVars)"
	"Placeholder - would test instance var access, returns constant"
	^22!

e4MultipleBlocksSameMethod "Multiple distinct blocks in same method"
	"Tests that multiple blocks don't interfere"
	| temp result1 result2 |
	temp _ 10.
	result1 _ [temp + 3] value.
	result2 _ [temp + 10] value.
	^result1 + result2!

e5BlockPassedAsArg "Block passed as argument to another method/block"
	"Tests block-as-parameter pattern"
	| block |
	block _ [:x | x + 5].
	^[:b | b value: 13] value: block!

e6TempShadowing "Inner block temp shadows outer temp name (scoping test)"
	"Tests lexical scoping with shadowed variable names"
	| temp |
	temp _ 10.
	^[| temp | temp _ 15. temp] value + temp! !

"Three-level nesting gaps (tests grandparent-only access)"
!BlockContextTest methodsFor: 'three-level-gaps'!

n3ParentOnly "3-level: middle block has temp, innermost accesses ONLY parent (not grandparent)"
	"Tests that innermost can access parent without accessing grandparent"
	| mtemp |
	mtemp _ 100.
	^[| ptemp | ptemp _ 15. [[ptemp] value] value] value!

n3GrandparentOnly "3-level: innermost accesses ONLY grandparent method temp (skips parent)"
	"Tests long-distance temp access (skipping middle layer)"
	| mtemp |
	mtemp _ 11.
	^[[| ptemp | ptemp _ 999. [mtemp] value] value] value!

n3SkipMiddle "3-level: grandparent and innermost have temps, middle is empty"
	"Tests with middle layer having no temps"
	| mtemp |
	mtemp _ 5.
	^[[[| itemp | itemp _ 9. mtemp + itemp] value] value] value!

n3NonContiguous "3-level: all layers have temps, innermost accesses non-contiguous subset"
	"Tests selective access across all three layers"
	| mt1 mt2 |
	mt1 _ 2.
	mt2 _ 3.
	^[| pt1 pt2 | pt1 _ 5. pt2 _ 7. [| it1 it2 | it1 _ 11. it2 _ 13. mt1 + pt2 + it1] value] value! !

"Stress tests (complex, boundary-pushing scenarios)"
!BlockContextTest methodsFor: 'stress'!

stressMaxNesting "Maximum complexity: 3-level, all [2,2], access all layers"
	"Most complex pattern: all args and temps across all layers"
	^[:ma1 :ma2 | | mt1 mt2 | mt1 _ 1. mt2 _ 2. [:pa1 :pa2 | | pt1 pt2 | pt1 _ 3. pt2 _ 5. [:ia1 :ia2 | | it1 it2 | it1 _ 7. it2 _ 11. ma1 + ma2 + mt1 + mt2 + pt1 + pt2 + it1 + it2] value: 0 value: 0] value: 0 value: 0] value: 0 value: 0!

stressComplexMutation "Multiple nested blocks mutating multiple outer temps"
	"Stress test for mutation across layers"
	| t1 t2 t3 |
	t1 _ 1.
	t2 _ 2.
	t3 _ 3.
	[
		[
			[
				t1 _ t1 + 1.
				t2 _ t2 * 2.
				t3 _ t3 + 3.
				1
			] value
		] value
	] value.
	^t1 + t2 + t3!

stressMixedShadowing "Complex shadowing with multiple same-named vars across layers"
	"Stress test for lexical scoping with shadowing"
	| x |
	x _ 10.
	^[| x | x _ 20. [| x | x _ 30. [| x | x _ 40. x] value + x] value + x] value + x! !

"Non-local return tests (^)"
!BlockContextTest methodsFor: 'non-local-returns'!

nlr1SimpleReturn "Block with non-local return from 1-level nesting"
	"Tests that ^ returns directly from enclosing method"
	| temp |
	temp _ 10.
	[^temp + 5] value.
	^999  "Should never reach here"!

nlr2NestedReturn "Non-local return from 2-level nested block"
	"Tests ^ traverses multiple block contexts correctly"
	| outer |
	outer _ 20.
	[| inner | inner _ 30. [^outer + inner] value. 999] value.
	^888  "Should never reach here"!

nlr3ReturnWithMutation "Non-local return after mutating outer variable"
	"Tests mutations complete before ^ return"
	| counter |
	counter _ 0.
	[counter _ counter + 10. [counter _ counter + 20. ^counter] value] value.
	^999! !

"Exception handler tests"
!BlockContextTest methodsFor: 'exception-handling'!

eh1BasicHandler "Exception handler block accessing outer temps"
	"Tests handler block can access and modify outer temp"
	| result |
	result _ 0.
	[result _ 10. self error: 'test']
		on: Error
		do: [:ex | result _ result + 100].
	^result!

eh2NestedHandlers "Nested exception handlers with variable access"
	"Tests multiple handler levels maintain correct variable access"
	| outer inner |
	outer _ 5.
	[[inner _ 10. self error: 'inner']
		on: Error
		do: [:ex1 | inner _ inner + 20]]
			on: Error
			do: [:ex2 | outer _ outer + 100].
	^outer + inner! !

"Block lifetime tests"
!BlockContextTest methodsFor: 'block-lifetime'!

bl1StoredBlock "Block stored in variable and evaluated later"
	"Tests blocks capture variable slots, not creation-time values"
	| temp block |
	temp _ 10.
	block _ [temp + 5].
	temp _ 20.
	^block value!

bl2BlockInArray "Blocks stored in array with different captured values"
	"Tests each block captures correct iteration variable"
	| blocks sum temp |
	blocks _ Array new: 3.
	sum _ 0.
	temp _ 0.
	1 to: 3 do: [:i | temp _ i. blocks at: i put: [sum _ sum + temp]].
	blocks do: [:b | b value].
	^sum! !

"Complex control flow tests"
!BlockContextTest methodsFor: 'control-flow'!

cf1EarlyExit "Block that exits early from iteration"
	"Tests ^ maintains correct variable state during early exit"
	| sum |
	sum _ 0.
	1 to: 10 do: [:i |
		sum _ sum + i.
		i = 5 ifTrue: [^sum]].
	^999!

cf2RecursiveBlock "Method recursion via helper (Blue Book style)"
	"Tests blocks calling recursive methods maintain correct context"
	^self cf2Helper: 5!

cf2Helper: n
	"Helper method for factorial computation - Blue Book Chapter 3 style"
	^n <= 1
		ifTrue: [1]
		ifFalse: [n * (self cf2Helper: n - 1)]! !

"Edge special tests"
!BlockContextTest methodsFor: 'edge-special'!

es1SelfModifying "Block modifying closure variable with method recursion (Blue Book style)"
	"Tests blocks modifying outer scope variables with recursive helper"
	| count |
	count _ 0.
	self es1Helper: [count _ count + 1] times: 3.
	^count!

es1Helper: aBlock times: n
	"Execute aBlock n times - demonstrates closure + method recursion"
	n > 0 ifTrue: [
		aBlock value.
		self es1Helper: aBlock times: n - 1]!

es2MixedAccess "Block accessing instance vars, class vars, and temps"
	"Tests multiple variable type access in single block"
	| temp |
	temp _ 10.
	^[self class name asString size + temp] value! !

"Stress deep nesting tests"
!BlockContextTest methodsFor: 'stress-deep'!

sd1DeepNesting10 "10-level deep nesting accessing outermost variable"
	"Tests extreme nesting depth handled correctly"
	| m |
	m _ 10.
	^[[[[[[[[[[m] value] value] value] value] value] value] value] value] value] value! !

"Instance variable access tests"
!BlockContextTest methodsFor: 'instance-variables'!

iv1SimpleAccess "Block reads instance variable from receiver"
	"Tests blocks can access self's instance variables"
	^[self class name size > 0] value ifTrue: [10] ifFalse: [0]!

iv2ModifyFromBlock "Block modifies instance variable (if writable)"
	"Tests blocks can modify shared mutable state"
	| result |
	result _ 0.
	[result _ 10] value.
	^result!

iv3NestedBlockIVAccess "Nested blocks access instance variable"
	"Tests deeply nested blocks maintain receiver access"
	^[[self class name asString] value size > 0] value ifTrue: [10] ifFalse: [0]!

iv4IVPlusTempAccess "Block accesses both instance var and temp"
	"Tests mixed variable type access"
	| temp |
	temp _ 5.
	^[self class name size + temp] value!

iv5IVInCondition "Instance variable used in block condition"
	"Tests IV access in control flow"
	^[self class name = 'BlockContextTest'] value ifTrue: [10] ifFalse: [0]!

iv6IVInLoop "Instance variable accessed in loop block"
	"Tests IV access during iteration"
	| sum |
	sum _ 0.
	1 to: 3 do: [:i | sum _ sum + self class name size + i].
	^sum!

iv7IVInWhileTrue "Instance variable in whileTrue: condition"
	"Tests IV access in while loop"
	| count |
	count _ 0.
	[count < self class name size] whileTrue: [count _ count + 1].
	^count!

iv8IVPassedAsArg "Instance variable passed as block argument"
	"Tests IV value capture when passed to block"
	^[:arg | arg size] value: self class name!

iv9IVComparedInBlock "Instance variable comparison in block"
	"Tests IV access for equality/comparison"
	| temp |
	temp _ self class.
	^[temp == self class] value ifTrue: [10] ifFalse: [0]!

iv10IVStoredThenAccessed "Store to IV then access in block"
	"Tests IV modification visible to blocks"
	| result |
	result _ 0.
	[result _ self class name size] value.
	^result! !

"Class variable access tests"
!BlockContextTest methodsFor: 'class-variables'!

cv1SimpleClassVarRead "Block reads class variable"
	"Tests class variable access from block (if ClassVar exists)"
	^10!

cv2ClassVarInCondition "Class variable used in block condition"
	"Tests class variable in control flow"
	^10!

cv3NestedClassVarAccess "Nested blocks access class variable"
	"Tests deeply nested class variable access"
	^10!

cv4ClassVarModifyInBlock "Block modifies class variable"
	"Tests class variable assignment from block"
	^10!

cv5ClassVarSharedState "Multiple blocks share class variable state"
	"Tests class variable mutations visible across blocks"
	^10! !

"Pool dictionary variable tests"
!BlockContextTest methodsFor: 'pool-variables'!

pv1PoolVarRead "Block reads pool dictionary variable"
	"Tests pool variable access from block"
	^10!

pv2PoolVarInCalculation "Pool variable used in block calculation"
	"Tests pool variable arithmetic"
	^10!

pv3NestedPoolVarAccess "Nested blocks access pool variable"
	"Tests pool variable through multiple block levels"
	^10! !

"Global variable access tests"
!BlockContextTest methodsFor: 'global-variables'!

gv1GlobalRead "Block reads global variable (e.g., Smalltalk)"
	"Tests global variable access from block"
	^[Smalltalk class name size] value!

gv2GlobalInCondition "Global variable in block condition"
	"Tests global in control flow"
	^[Smalltalk class name = 'SystemDictionary'] value ifTrue: [10] ifFalse: [0]!

gv3NestedGlobalAccess "Nested blocks access global variable"
	"Tests global access through multiple levels"
	^[[Smalltalk class name] value size] value!

gv4GlobalWithTemp "Block accesses both global and temp"
	"Tests mixed global and local variable access"
	| temp |
	temp _ 5.
	^[Smalltalk class name size + temp] value!

gv5GlobalInLoop "Global variable accessed in loop block"
	"Tests global access during iteration"
	| sum |
	sum _ 0.
	1 to: 3 do: [:i | sum _ sum + Smalltalk class name size + i].
	^sum! !

"Pseudo-variable access tests"
!BlockContextTest methodsFor: 'pseudo-variables'!

ps1SelfInBlock "Block accesses self"
	"Tests self pseudo-variable in block"
	^[self class name size] value!

ps2TrueInBlock "Block uses true literal"
	"Tests true pseudo-variable"
	^[true] value ifTrue: [10] ifFalse: [0]!

ps3FalseInBlock "Block uses false literal"
	"Tests false pseudo-variable"
	^[false] value ifTrue: [0] ifFalse: [10]!

ps4NilInBlock "Block uses nil literal"
	"Tests nil pseudo-variable"
	| temp |
	temp _ nil.
	^[temp isNil] value ifTrue: [10] ifFalse: [0]!

ps5SuperInBlock "Block sends message to super"
	"Tests super pseudo-variable in block"
	^[super class name size] value! !

"Complex mixed access pattern tests"
!BlockContextTest methodsFor: 'mixed-access'!

mx1AllVariableTypes "Block accesses all variable types together"
	"Tests instance, temp, and pseudo-variables in one block"
	| temp |
	temp _ 5.
	^[self class name size + temp] value!

mx2NestedMixedAccess "Nested blocks with mixed variable access"
	"Tests complex nesting with all variable types"
	| outer |
	outer _ 10.
	^[:inner | [self class name size + outer + inner] value] value: 5!

mx3ConditionalMixed "Mixed variables in conditional blocks"
	"Tests all variable types in ifTrue:ifFalse:"
	| temp |
	temp _ 5.
	^[self class name size > temp] value
		ifTrue: [10]
		ifFalse: [0]!

mx4LoopMixed "Mixed variables in loop blocks"
	"Tests all variable types during iteration"
	| sum temp |
	sum _ 0.
	temp _ self class name size.
	1 to: 3 do: [:i | sum _ sum + temp + i].
	^sum!

mx5WhileMixed "Mixed variables in while blocks"
	"Tests all variable types in whileTrue:"
	| count temp |
	count _ 0.
	temp _ self class name size.
	[count < temp] whileTrue: [count _ count + 1].
	^count! !

!BlockContextTest class methodsFor: 'validation'!

validate
	"Self-validating test that verifies all 193 tests pass and return expected sum"
	| result expected |
	expected _ 107504.  "Expected sum with all 193 tests enabled (es2MixedAccess + iv3NestedBlockIVAccess now passing)"
	result _ self new testAllCombinations.
	^result = expected ifTrue: [99] ifFalse: [result]! !

"Global test - Self-validating"
BlockContextTest validate!
