factorial
    self = 0 ifTrue: [^1].
    ^self * (self - 1) factorial