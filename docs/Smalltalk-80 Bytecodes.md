### The Smalltalk-80 Bytecodes

| Range     | Bits          | Function                                                                 |
|-----------|---------------|--------------------------------------------------------------------------|
| 0–15      | 000 0 i i i i | Push Receiver Variable #iiii                                             |
| 16–31     | 000 1 i i i i | Push Temporary Location #iiii                                           |
| 32–63     | 001 0 i i i i | Push Literal Constant #iiii                                             |
| 64–95     | 001 1 i i i i | Push Literal Variable #iiii                                             |
| 96–103    | 011 0 0 i i i | Pop and Store Receiver Variable #iii                                    |
| 104–111   | 011 0 1 i i i | Pop and Store Temporary Location #iii                                   |
| 112–119   | 011 1 0 i i i | Push (receiver, true, false, nil, -1, 0, 1, 2) [i i i]                    |
| 120–123   | 011 1 1 0 i i | Return (receiver, true, false, nil) [i i] From Message                   |
| 124–125   | 011 1 1 1 0 i | Return Stack Top From (Message, Block) [i]                               |
| 126–127   | 011 1 1 1 1 1 | unused                                                                   |
| 128       | 10000000      | Push (Receiver Variable, Temporary Location, Literal Constant, Literal Variable) [j j] #kkkkkk |
| 129       | 10000001      | Store (Receiver Variable, Temporary Location, Illegal, Literal Variable) [j j] #kkkkkk |
| 130       | 10000010      | Pop and Store (Receiver Variable, Temporary Location, Illegal, Literal Variable) [j j] #kkkkkk |
| 131       | 10000011      | Send Literal Selector #kkkkk With j j j Arguments                        |
| 132       | 10000100      | Send Literal Selector #kkkkkkkk With j j j j j j j j Arguments           |
| 133       | 10000101      | Send Literal Selector #kkkkk To Superclass With j j j Arguments          |
| 134       | 10000110      | Send Literal Selector #kkkkkkkk To Superclass With j j j j j j j j Arguments |
| 135       | 10000111      | Pop Stack Top                                                            |
| 136       | 10001000      | Duplicate Stack Top                                                      |
| 137       | 10001001      | Push Active Context                                                      |
| 138–143   |               | unused                                                                   |
| 144–151   | 100100ii      | Jump i i i + 1 (i.e., 1 through 8)                                       |
| 152–159   | 100101ii      | Pop and Jump On False i i i + 1 (i.e., 1 through 8)                      |
| 160–167   | 101000ii      | Jump (i i i + 4) * 256 + j j j j j j j j                                 |
| 168–171   | 101010ii      | Pop and Jump On True i i * 256 + j j j j j j j j                         |
| 172–175   | 101011ii      | Pop and Jump On False i i * 256 + j j j j j j j j                        |
| 176–191   | 1011iiii      | Send Arithmetic Message #iiii                                            |
| 192–207   | 1100iiii      | Send Special Message #iiii                                               |
| 208–223   | 1101iiii      | Send Literal Selector #iiii With No Arguments                           |
| 224–239   | 1110iiii      | Send Literal Selector #iiii With 1 Argument                             |
| 240–255   | 1111iiii      | Send Literal Selector #iiii With 2 Arguments                            |
