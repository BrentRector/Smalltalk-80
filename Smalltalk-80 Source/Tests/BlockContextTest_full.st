"BlockContextTest - Comprehensive validation of nested block context variable access and mutation"

Object subclass: #BlockContextTest
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Tests-BlockContext'! !

!BlockContextTest methodsFor: 'testing'!

testAllCombinations
	"Test all single-level (9), two-level (81), three-level (27), and mutation tests"
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
	"Mutation tests: 3 tests"
	sum _ sum + (self mut1).
	sum _ sum + (self mut2).
	sum _ sum + (self mut3).
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
	^[:oa | | ot1 ot2 | ot1 _ 0. ot2 _ 1. [:ma | | mt1 mt2 | mt1 _ 2. mt2 _ 3. [:ia | | it1 it2 | it1 _ 2. it2 _ 2. oa + ot1 + ot2 + ma + mt1 + mt2 + ia + it1 + it2] value: 0] value: 0] value: 0!

n121212diag "DIAGNOSTIC: Use powers of 2 to identify which variables are accessed"
	"Expected result: 511 (binary 111111111 = all 9 variables accessed)
	 Bit 0 (1):   oa  = 1
	 Bit 1 (2):   ot1 = 2
	 Bit 2 (4):   ot2 = 4
	 Bit 3 (8):   ma  = 8
	 Bit 4 (16):  mt1 = 16
	 Bit 5 (32):  mt2 = 32
	 Bit 6 (64):  ia  = 64
	 Bit 7 (128): it1 = 128
	 Bit 8 (256): it2 = 256"
	^[:oa | | ot1 ot2 | ot1 _ 2. ot2 _ 4. [:ma | | mt1 mt2 | mt1 _ 16. mt2 _ 32. [:ia | | it1 it2 | it1 _ 128. it2 _ 256. oa + ot1 + ot2 + ma + mt1 + mt2 + ia + it1 + it2] value: 64] value: 8] value: 1!

diagOa  "Return oa (expect 11)"  ^[:oa | | ot1 ot2 | ot1 _ 22. ot2 _ 44. [:ma | | mt1 mt2 | mt1 _ 166. mt2 _ 322. [:ia | | it1 it2 | it1 _ 188. it2 _ 266. oa] value: 644] value: 88] value: 11!
diagOt1 "Return ot1 (expect 22)" ^[:oa | | ot1 ot2 | ot1 _ 22. ot2 _ 44. [:ma | | mt1 mt2 | mt1 _ 166. mt2 _ 322. [:ia | | it1 it2 | it1 _ 188. it2 _ 266. ot1] value: 644] value: 88] value: 11!
diagOt2 "Return ot2 (expect 44)" ^[:oa | | ot1 ot2 | ot1 _ 22. ot2 _ 44. [:ma | | mt1 mt2 | mt1 _ 166. mt2 _ 322. [:ia | | it1 it2 | it1 _ 188. it2 _ 266. ot2] value: 644] value: 88] value: 11!
diagMa  "Return ma (expect 88)"  ^[:oa | | ot1 ot2 | ot1 _ 22. ot2 _ 44. [:ma | | mt1 mt2 | mt1 _ 166. mt2 _ 322. [:ia | | it1 it2 | it1 _ 188. it2 _ 266. ma] value: 644] value: 88] value: 11!
diagMt1 "Return mt1 (expect 166)" ^[:oa | | ot1 ot2 | ot1 _ 22. ot2 _ 44. [:ma | | mt1 mt2 | mt1 _ 166. mt2 _ 322. [:ia | | it1 it2 | it1 _ 188. it2 _ 266. mt1] value: 644] value: 88] value: 11!
diagMt2 "Return mt2 (expect 322)" ^[:oa | | ot1 ot2 | ot1 _ 22. ot2 _ 44. [:ma | | mt1 mt2 | mt1 _ 166. mt2 _ 322. [:ia | | it1 it2 | it1 _ 188. it2 _ 266. mt2] value: 644] value: 88] value: 11!
diagIa  "Return ia (expect 644)"  ^[:oa | | ot1 ot2 | ot1 _ 22. ot2 _ 44. [:ma | | mt1 mt2 | mt1 _ 166. mt2 _ 322. [:ia | | it1 it2 | it1 _ 188. it2 _ 266. ia] value: 644] value: 88] value: 11!
diagIt1 "Return it1 (expect 188)" ^[:oa | | ot1 ot2 | ot1 _ 22. ot2 _ 44. [:ma | | mt1 mt2 | mt1 _ 166. mt2 _ 322. [:ia | | it1 it2 | it1 _ 188. it2 _ 266. it1] value: 644] value: 88] value: 11!
diagIt2 "Return it2 (expect 266)" ^[:oa | | ot1 ot2 | ot1 _ 22. ot2 _ 44. [:ma | | mt1 mt2 | mt1 _ 166. mt2 _ 322. [:ia | | it1 it2 | it1 _ 188. it2 _ 266. it2] value: 644] value: 88] value: 11!

diagPow10 "Powers of 10: each var unique magnitude (expect 111111111)"
	"oa=1, ot1=10, ot2=100, ma=1000, mt1=10000, mt2=100000, ia=1000000, it1=10000000, it2=100000000
	 Digits reveal WHICH variables accessed AND which were skipped:
	   111111111 = all correct (all 9 variables)
	   111111011 = ot2 (100) NOT accessed, others OK
	   111011111 = mt2 (100000) NOT accessed, others OK
	   111111200 = ot2 accessed TWICE (100+100=200), it1+it2 skipped
	   100000000 = ONLY it2 accessed, all others zero
	   111111110 = it2 aliased to ot1 (read as 10 instead of 100000000)"
	^[:oa | | ot1 ot2 | ot1 _ 10. ot2 _ 100. [:ma | | mt1 mt2 | mt1 _ 10000. mt2 _ 100000. [:ia | | it1 it2 | it1 _ 10000000. it2 _ 100000000. oa + ot1 + ot2 + ma + mt1 + mt2 + ia + it1 + it2] value: 1000000] value: 1000] value: 1!

diagZero "Same as diagPow10 but with ZEROS for args (expect 111111111)"
	"Test if zeros cause the bug. Args set to 0, temps use powers of 10.
	 oa=0, ot1=10, ot2=100, ma=0, mt1=10000, mt2=100000, ia=0, it1=10000000, it2=100000000
	 Expected: 110110110 (only temps, no args)
	 If we get 111111111, zeros are being read as their power-of-10 value somehow
	 If we get 110110100, last digit wrong (it2 issue with zeros)"
	^[:oa | | ot1 ot2 | ot1 _ 10. ot2 _ 100. [:ma | | mt1 mt2 | mt1 _ 10000. mt2 _ 100000. [:ia | | it1 it2 | it1 _ 10000000. it2 _ 100000000. oa + ot1 + ot2 + ma + mt1 + mt2 + ia + it1 + it2] value: 0] value: 0] value: 0! !

"Mutation tests"
!BlockContextTest methodsFor: 'mutation'!

mut1 "Block modifies method temp"
	| m | m _ 5. [m _ m + 5] value. ^m!

mut2 "Inner modifies outer temp"
	^[| t | t _ 5. [t _ t + 5] value. t] value!

mut3 "Args immutable: inner sees original"
	^[:a | [:b | a] value: 999] value: 10! !

"Global test"
BlockContextTest new testAllCombinations!
