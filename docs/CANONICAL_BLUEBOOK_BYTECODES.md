# Canonical Smalltalk-80 Bytecodes (1983 Blue Book)

## Bytecode Ranges and Meanings

| Opcode Range | Bytecode Name | Description |
|--------------|---------------|-------------|
| 0–15 | pushReceiverVariable: | Push receiver's inst var (index 0–15) |
| 16–31 | pushTemporaryVariable: | Push temp var (index 0–15) |
| 32–63 | pushLiteralConstant: | Push literal constant (index 0–31) |
| 64–95 | pushLiteralVariable: | Push literal variable (index 0–31) |
| 96–103 | storeAndPopReceiverVariable: | Store top of stack into receiver var (0–7) |
| 104–111 | storeAndPopTemporaryVariable: | Store top of stack into temp var (0–7) |
| 112–119 | pushReceiver | Push receiver object |
| 120–127 | pushConstant | Push constant (e.g., nil, true, false, -1 to 2) |
| 128–134 | pushActiveContext | Push context-related objects |
| 135 | popStack | Discard top of stack |
| 136–143 | returnReceiver / returnTop | Return from method or block |
| 144–151 | sendSelector: | Send selector with 0 args (literal index 0–7) |
| 152–159 | sendSelector: | Send selector with 1 arg (literal index 0–7) |
| 160–167 | sendSelector: | Send selector with 2 args (literal index 0–7) |
| 168–175 | sendSelector: | Send selector with 3 args (literal index 0–7) |
| 176–191 | sendLiteralSelector: | Send literal selector with 0 args (index 0–15) |
| 192–207 | sendLiteralSelector: | Send literal selector with 1 arg (index 0–15) |
| 208–223 | sendLiteralSelector: | Send literal selector with 2 args (index 0–15) |
| 224–255 | unused or reserved | Reserved for future use or extensions |

## Notes
- **No extended sends**: The original VM only supports selectors with up to 2 arguments via short sends. Anything beyond that requires a fallback to extended bytecodes, which were introduced later.
- **Literal frame indexing**: All literal indices are 0-based and refer to entries in the method's literal frame.
- **Bytecode 135 (popStack)**: Often overlooked because the Blue Book doesn't name it "popStack" - it simply describes it as "Discard top of stack" in Appendix C. It's the only opcode in the 128–135 range that doesn't push something; it's the inverse of the others in that block. Not commonly used in most compiled methods since Smalltalk favors assignment to temps or receiver vars, which implicitly pop.
- **Opcodes 224–255**: These were unused in the original Blue Book spec. Later implementations (e.g., Squeak, VisualWorks) repurposed them for extended sends, closures, etc.