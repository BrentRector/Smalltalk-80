using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// Time primitives (98-100) per Blue Book specification.
/// - 98: secondClockInto: - Store seconds since 1901 into ByteArray
/// - 99: millisecondClockInto: - Store milliseconds into ByteArray
/// - 100: signal:atMilliseconds: - Schedule semaphore signal at future time
/// </summary>
public static class TimeOperations
{
    // Smalltalk epoch: January 1, 1901 00:00:00 UTC
    // Unix epoch: January 1, 1970 00:00:00 UTC
    // Difference: 2177452800 seconds (69 years, including 17 leap years)
    private static readonly long SmalltalkEpochOffset = 2177452800L;

    /// <summary>Primitive 98: secondClockInto: - Store seconds since Jan 1, 1901 into ByteArray</summary>
    /// <remarks>
    /// The argument is a byte indexable object of length at least four.
    /// Store into the first four bytes the number of seconds since
    /// 00:00 in the morning of January 1, 1901 (a 32-bit unsigned number).
    /// The low-order 8-bits are stored in byte index 1 (0-based: 0),
    /// the high-order 8-bits in byte index 4 (0-based: 3).
    /// This is LITTLE-ENDIAN byte order.
    /// </remarks>
    public class SecondClockInto : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);

            var byteArrayOOP = context.Arguments[0];
            if (byteArrayOOP.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            // Verify it's byte-indexable and has at least 4 bytes
            int size = context.HeapManager.GetIndexedSize(byteArrayOOP);
            if (size < 4) return PrimitiveResult.Fail(context.Metadata!);

            // Get current LOCAL time and convert to Smalltalk epoch.
            // The Smalltalk code has hardcoded timezone parameters for California (Pacific, UTC-8)
            // that subtract 8 hours. To compensate, we add 8 hours to local time so that
            // after Smalltalk's subtraction, the result is correct local time.
            DateTime localNow = DateTime.Now;
            DateTime smalltalkEpoch = new DateTime(1901, 1, 1, 0, 0, 0, DateTimeKind.Local);
            long smalltalkSeconds = (long)(localNow - smalltalkEpoch).TotalSeconds;
            // Add 8 hours to compensate for Smalltalk's Pacific timezone subtraction
            smalltalkSeconds += 8 * 3600;

            // Store as 32-bit unsigned in little-endian order
            uint seconds32 = (uint)(smalltalkSeconds & 0xFFFFFFFF);
            context.HeapManager.SetIndexedByte(byteArrayOOP, 0, (byte)(seconds32 & 0xFF));
            context.HeapManager.SetIndexedByte(byteArrayOOP, 1, (byte)((seconds32 >> 8) & 0xFF));
            context.HeapManager.SetIndexedByte(byteArrayOOP, 2, (byte)((seconds32 >> 16) & 0xFF));
            context.HeapManager.SetIndexedByte(byteArrayOOP, 3, (byte)((seconds32 >> 24) & 0xFF));

            return PrimitiveResult.Ok(context.Receiver);
        }
    }

    /// <summary>Primitive 99: millisecondClockInto: - Store milliseconds into ByteArray</summary>
    /// <remarks>
    /// The argument is a byte indexable object of length at least four.
    /// Store into the first four bytes the number of milliseconds since
    /// the millisecond clock was last reset or rolled over (a 32-bit unsigned number).
    /// The low-order 8-bits are stored in byte index 1 (0-based: 0),
    /// the high-order 8-bits in byte index 4 (0-based: 3).
    /// This is LITTLE-ENDIAN byte order.
    ///
    /// Note: This is a relative clock that wraps around every ~49.7 days.
    /// We use Environment.TickCount64 modulo 2^32.
    /// </remarks>
    public class MillisecondClockInto : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);

            var byteArrayOOP = context.Arguments[0];
            if (byteArrayOOP.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            // Verify it's byte-indexable and has at least 4 bytes
            int size = context.HeapManager.GetIndexedSize(byteArrayOOP);
            if (size < 4) return PrimitiveResult.Fail(context.Metadata!);

            // Get milliseconds since system start (wraps every ~49.7 days)
            uint milliseconds = (uint)(Environment.TickCount64 & 0xFFFFFFFF);

            // Store as 32-bit unsigned in little-endian order
            context.HeapManager.SetIndexedByte(byteArrayOOP, 0, (byte)(milliseconds & 0xFF));
            context.HeapManager.SetIndexedByte(byteArrayOOP, 1, (byte)((milliseconds >> 8) & 0xFF));
            context.HeapManager.SetIndexedByte(byteArrayOOP, 2, (byte)((milliseconds >> 16) & 0xFF));
            context.HeapManager.SetIndexedByte(byteArrayOOP, 3, (byte)((milliseconds >> 24) & 0xFF));

            return PrimitiveResult.Ok(context.Receiver);
        }
    }

    /// <summary>Primitive 100: signal:atMilliseconds: - Schedule semaphore signal at future time</summary>
    /// <remarks>
    /// Signal the semaphore when the millisecond clock reaches the value of
    /// the second argument. The second argument is a byte indexable object at
    /// least four bytes long (a 32-bit unsigned number with the low order
    /// 8-bits stored in the byte with the lowest index).
    /// Fail if the first argument is neither a Semaphore nor nil.
    ///
    /// If the semaphore is nil, cancel any pending timer.
    ///
    /// This is the foundation of the Delay mechanism in Smalltalk.
    /// </remarks>
    public class SignalAtMilliseconds : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 2) return PrimitiveResult.Fail(context.Metadata!);

            var semaphoreOOP = context.Arguments[0];
            var millisecondsOOP = context.Arguments[1];

            // Semaphore must be nil or a Semaphore object
            if (semaphoreOOP.IsSmallInteger && semaphoreOOP != context.HeapManager.GetNil())
                return PrimitiveResult.Fail(context.Metadata!);

            // If semaphore is nil, cancel pending timer
            if (semaphoreOOP == context.HeapManager.GetNil())
            {
                context.TimerManager?.CancelTimer();
                return PrimitiveResult.Ok(context.Receiver);
            }

            // Milliseconds argument must be byte-indexable with at least 4 bytes
            if (millisecondsOOP.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            int size = context.HeapManager.GetIndexedSize(millisecondsOOP);
            if (size < 4) return PrimitiveResult.Fail(context.Metadata!);

            // Read 32-bit milliseconds value (little-endian)
            uint targetMs = (uint)context.HeapManager.GetIndexedByte(millisecondsOOP, 0)
                          | ((uint)context.HeapManager.GetIndexedByte(millisecondsOOP, 1) << 8)
                          | ((uint)context.HeapManager.GetIndexedByte(millisecondsOOP, 2) << 16)
                          | ((uint)context.HeapManager.GetIndexedByte(millisecondsOOP, 3) << 24);

            // Schedule the timer
            if (context.TimerManager == null)
                return PrimitiveResult.Fail(context.Metadata!);

            context.TimerManager.ScheduleSignal(semaphoreOOP, targetMs);
            return PrimitiveResult.Ok(context.Receiver);
        }
    }
}
