using System;
using System.Diagnostics;
using System.Threading;

namespace UmaMusumeAudio
{
    // Explicit, bounded diagnostics around the existing streaming callbacks.
    // Positions are decoder-stream bytes, not speaker positions or an audio epoch.
    public sealed class StreamingAudioCapture : IDisposable
    {
        // The view is valid only for this synchronous observation. It cannot
        // modify the audio data or escape through a managed field.
        public delegate void ReadCompletedHandler(Callback callback, ReadOnlySpan<float> samples);
        public event ReadCompletedHandler ReadCompleted;
        [Serializable]
        public struct Callback
        {
            public int serial, kind, thread, requested, returned;
            public long beginTicks, endTicks, beforeBytes, afterBytes;
            public bool failed;
        }

        [Serializable]
        public sealed class Snapshot
        {
            public string schema = "ViewerStreamingCallbacks-v1", resource;
            public int waveId, clipId, clipSamples, channels, frequency, bytesPerSample;
            public int decoderDelay, decoderPadding, decoderSamplesPerBlock, decoderSampleCount;
            public long streamLengthBytes, timestampFrequency;
            public int reads, sets, errors, overlaps, overflow, duplicateAttachments, inFlight, observerErrors;
            public bool closed;
            public Callback[] callbacks;
        }

        static StreamingAudioCapture current;
        readonly string resource;
        readonly int waveId;
        readonly Callback[] rows;
        readonly int[] published;
        int enabled = 1, attached, next, last = -1, active;
        int reads, sets, errors, overlaps, overflow, duplicates;
        int observerErrors, activeObservers;
        int clipId, clipSamples, channels, frequency, bytesPerSample;
        int decoderDelay, decoderPadding, decoderSamplesPerBlock, decoderSampleCount;
        long length;
        Func<long> position;
        Func<float[], int> read;
        Action<int> setPosition;

        StreamingAudioCapture(string resource, int waveId, int capacity)
        { this.resource = resource; this.waveId = waveId; rows = new Callback[capacity]; published = new int[capacity]; }

        // Register before loading exactly one selected wave. No corpus enumeration.
        public static StreamingAudioCapture Register(string resource, int waveId, int capacity = 32768)
        {
            if (string.IsNullOrEmpty(resource) || waveId < 0 || capacity < 1 || capacity > 65536)
                throw new ArgumentException("An explicit resource, wave and bounded capacity are required.");
            var capture = new StreamingAudioCapture(resource, waveId, capacity);
            if (Interlocked.CompareExchange(ref current, capture, null) != null)
                throw new InvalidOperationException("A streaming observation is already registered.");
            return capture;
        }

        public static StreamingAudioCapture TryAttach(string resource, int waveId, int channels,
            int bytesPerSample, int frequency, long length, Func<long> position,
            Func<float[], int> read, Action<int> setPosition)
        {
            var capture = Volatile.Read(ref current);
            if (capture == null || Volatile.Read(ref capture.enabled) == 0 ||
                capture.resource != resource || capture.waveId != waveId) return null;
            if (channels < 1 || bytesPerSample < 1 || frequency < 1 || length < 0 ||
                position == null || read == null || setPosition == null)
                throw new ArgumentException("Current stream format and callbacks are required.");
            if (Interlocked.CompareExchange(ref capture.attached, 1, 0) != 0)
            { Interlocked.Increment(ref capture.duplicates); return null; }
            capture.channels = channels; capture.bytesPerSample = bytesPerSample;
            capture.frequency = frequency; capture.length = length;
            capture.position = position; capture.read = read; capture.setPosition = setPosition;
            return capture;
        }

        // Called on the main thread after AudioClip.Create returns. Earlier
        // callbacks already belong to this token; no Unity API runs in them.
        public void BindClip(int id, int samples, int clipChannels, int clipFrequency)
        {
            if (id == 0 || clipId != 0 || samples < 0 || clipChannels != channels || clipFrequency != frequency)
                throw new ArgumentException("The returned clip must match this attached stream.");
            clipSamples = samples; Volatile.Write(ref clipId, id);
        }

        public bool MatchesClip(int id) => id != 0 && Volatile.Read(ref clipId) == id;

        // Main-thread values from the already-created decoder, never a second
        // parse. They describe its logical stream origin; they are not latency.
        public void DescribeDecoder(int delay, int padding, int samplesPerBlock, int sampleCount)
        {
            if (delay < 0 || padding < 0 || samplesPerBlock < 1 || sampleCount < 0 || decoderSamplesPerBlock != 0 ||
                (long)sampleCount * channels * bytesPerSample != length)
                throw new ArgumentException("Current decoder description must match this stream.");
            decoderDelay = delay; decoderPadding = padding; decoderSamplesPerBlock = samplesPerBlock; decoderSampleCount = sampleCount;
        }

        bool Begin(int kind, int requested, out Callback row, out int index)
        {
            row = default; index = -1;
            if (Volatile.Read(ref enabled) == 0) return false;
            int concurrent = Interlocked.Increment(ref active);
            if (Volatile.Read(ref enabled) == 0) { Interlocked.Decrement(ref active); return false; }
            if (concurrent != 1) Interlocked.Increment(ref overlaps);
            index = Interlocked.Increment(ref next) - 1;
            if (index >= rows.Length)
            { Interlocked.Increment(ref overflow); Interlocked.Decrement(ref active); return false; }
            row.serial = index + 1; row.kind = kind; row.requested = requested;
            row.thread = Thread.CurrentThread.ManagedThreadId; row.beginTicks = Stopwatch.GetTimestamp();
            row.beforeBytes = Position(ref row);
            return true;
        }

        long Position(ref Callback row)
        {
            try { return position(); }
            catch { row.failed = true; return -1; } // Observation must not replace the real callback.
        }

        void Complete(ref Callback row, int index)
        {
            row.afterBytes = Position(ref row); row.endTicks = Stopwatch.GetTimestamp();
            if (row.failed) Interlocked.Increment(ref errors);
            if (row.kind == 1) Interlocked.Increment(ref reads); else Interlocked.Increment(ref sets);
            rows[index] = row; Volatile.Write(ref published[index], 1);
            int previous;
            do { previous = Volatile.Read(ref last); if (previous >= index) break; }
            while (Interlocked.CompareExchange(ref last, index, previous) != previous);
            Interlocked.Decrement(ref active);
        }

        public void Read(float[] data)
        {
            bool recording = Begin(1, data.Length, out var row, out int index);
            try { int returned = read(data); if (recording) row.returned = returned; }
            catch { if (recording) row.failed = true; throw; }
            finally
            {
                if (recording)
                {
                    var observer = row.failed ? null : ReadCompleted;
                    if (observer != null) Interlocked.Increment(ref activeObservers);
                    try
                    {
                        Complete(ref row, index);
                        if (observer != null)
                            try { observer(row, data); }
                            catch { Interlocked.Increment(ref observerErrors); }
                    }
                    finally { if (observer != null) Interlocked.Decrement(ref activeObservers); }
                }
            }
        }

        public void SetPosition(int sample)
        {
            bool recording = Begin(2, sample, out var row, out int index);
            try { setPosition(sample); }
            catch { if (recording) row.failed = true; throw; }
            finally { if (recording) Complete(ref row, index); }
        }

        public int LastCompletedSerial
        {
            get { int index = Volatile.Read(ref last); return index < 0 ? 0 : rows[index].serial; }
        }

        // Stop first; a caller must reject inFlight != 0 rather than treating a
        // partial callback as a completed position. No waiting on the audio thread.
        public Snapshot StopAndSnapshot()
        {
            Dispose();
            int count = Math.Min(Volatile.Read(ref next), rows.Length);
            var copy = new Callback[count];
            for (int i = 0; i < count; i++) if (Volatile.Read(ref published[i]) != 0) copy[i] = rows[i];
            return new Snapshot { resource = resource, waveId = waveId, clipId = clipId, clipSamples = clipSamples,
                channels = channels, frequency = frequency, bytesPerSample = bytesPerSample, streamLengthBytes = length,
                decoderDelay = decoderDelay, decoderPadding = decoderPadding,
                decoderSamplesPerBlock = decoderSamplesPerBlock, decoderSampleCount = decoderSampleCount,
                timestampFrequency = Stopwatch.Frequency, reads = Volatile.Read(ref reads), sets = Volatile.Read(ref sets),
                errors = Volatile.Read(ref errors), overlaps = Volatile.Read(ref overlaps), overflow = Volatile.Read(ref overflow),
                duplicateAttachments = Volatile.Read(ref duplicates), inFlight = Volatile.Read(ref active) + Volatile.Read(ref activeObservers),
                observerErrors = Volatile.Read(ref observerErrors), closed = true, callbacks = copy };
        }

        public void Dispose()
        { Interlocked.Exchange(ref enabled, 0); Interlocked.CompareExchange(ref current, null, this); }
    }
}
