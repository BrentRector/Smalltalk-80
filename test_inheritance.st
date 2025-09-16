Object subclass: #TestParent instanceVariableNames: \"field1 field2\" classVariableNames: \"\" poolDictionaries: \"\" category: \"Test\"\!

\!TestParent methodsFor: \"accessing\"\!
getField1
    ^field1\!

setField1: value
    field1 := value\!
\! \!

TestParent subclass: #TestChild instanceVariableNames: \"field3\" classVariableNames: \"\" poolDictionaries: \"\" category: \"Test\"\!

\!TestChild methodsFor: \"accessing\"\!
getField3
    ^field3\!

setField3: value
    field3 := value\!

useAllFields
    ^field1 + field2 + field3\!
\! \!
