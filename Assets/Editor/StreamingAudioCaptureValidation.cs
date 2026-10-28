using System;
using System.IO;
using System.Threading;
using UmaMusumeAudio;
using UnityEditor;
using UnityEngine;

// Artificial callback transparency/publication checks. No media or speaker epoch.
public static class StreamingAudioCaptureValidation
{
    [Serializable] sealed class Report { public string status, failure; public int checks; }
    static Report report;
    static void Check(bool pass, string message) { report.checks++; if (!pass) throw new Exception(message); }
    static void Reject(Action action)
    { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } Check(rejected, "Invalid input accepted."); }
    static void Transparency()
    {
        long position = 0; int reads = 0, sets = 0;
        Func<float[], int> reader = data => { reads++; for (int i = 0; i < data.Length; i++) data[i] = i + .25f; position += data.Length * 2; return data.Length; };
        using (var capture = StreamingAudioCapture.Register("artificial", 3))
        {
            Check(StreamingAudioCapture.TryAttach("foreign", 3, 2, 2, 100, 1000, () => position, reader, _ => { }) == null, "Foreign resource remains unobserved.");
            Check(StreamingAudioCapture.TryAttach("artificial", 4, 2, 2, 100, 1000, () => position, reader, _ => { }) == null, "Foreign wave remains unobserved.");
            Check(StreamingAudioCapture.TryAttach("artificial", 3, 2, 2, 100, 1000, () => position, reader, p => { sets++; position = p * 4; }) == capture, "Exact stream attached.");
            var data = new float[8]; capture.Read(data); // Can happen inside AudioClip.Create, before BindClip.
            Check(reads == 1 && position == 16, "Exactly one underlying read.");
            for (int i = 0; i < data.Length; i++) Check(data[i] == i + .25f, "Returned data preserved.");
            capture.SetPosition(7); Check(sets == 1 && position == 28, "Set forwarded once in original units.");
            capture.BindClip(51, 250, 2, 100);
            Reject(() => capture.DescribeDecoder(3, 1, 8, 249));
            capture.DescribeDecoder(3, 1, 8, 250);
            Reject(() => capture.DescribeDecoder(3, 1, 8, 250));
            Check(capture.MatchesClip(51) && !capture.MatchesClip(52) && !capture.MatchesClip(0), "Explicit clip identity.");
            Check(capture.LastCompletedSerial == 2, "Only completed callbacks published.");
            var saved = capture.StopAndSnapshot(); var first = saved.callbacks[0]; var second = saved.callbacks[1];
            Check(saved.inFlight == 0 && saved.errors == 0 && saved.overlaps == 0 && saved.overflow == 0, "Complete observation.");
            Check(saved.callbacks.Length == 2 && saved.reads == 1 && saved.sets == 1, "Each callback captured once.");
            Check(saved.decoderDelay == 3 && saved.decoderPadding == 1 && saved.decoderSamplesPerBlock == 8 && saved.decoderSampleCount == 250, "Current decoder description retained independently of callback coordinates.");
            Check(first.beforeBytes == 0 && first.afterBytes == 16 && first.returned == 8 && first.requested == 8, "Actual read positions.");
            Check(second.beforeBytes == 16 && second.afterBytes == 28 && second.requested == 7 && second.kind == 2, "Actual set positions.");
            Check(first.beginTicks <= first.endTicks && first.endTicks <= second.beginTicks && saved.timestampFrequency > 0, "Timestamp brackets.");
            capture.Read(data); capture.SetPosition(0);
            Check(reads == 2 && sets == 2 && saved.callbacks.Length == 2, "Disposal preserves playback and saved data.");
        }
    }
    static void Boundaries()
    {
        Reject(() => StreamingAudioCapture.Register("", 0)); Reject(() => StreamingAudioCapture.Register("a", -1));
        Reject(() => StreamingAudioCapture.Register("a", 0, 0)); Reject(() => StreamingAudioCapture.Register("a", 0, 65537));
        using (var capture = StreamingAudioCapture.Register("a", 0, 1))
        {
            bool duplicate = false; try { StreamingAudioCapture.Register("b", 0); } catch (InvalidOperationException) { duplicate = true; }
            Check(duplicate, "Existing registration ownership retained.");
            Reject(() => StreamingAudioCapture.TryAttach("a", 0, 0, 2, 100, 100, () => 0, _ => 0, _ => { }));
            int calls = 0;
            StreamingAudioCapture.TryAttach("a", 0, 1, 2, 100, 100, () => 0, _ => { calls++; return 0; }, _ => { });
            Check(StreamingAudioCapture.TryAttach("a", 0, 1, 2, 100, 100, () => 0, _ => 0, _ => { }) == null, "Duplicate stream not rebound.");
            Reject(() => capture.BindClip(1, 50, 2, 100)); capture.BindClip(1, 50, 1, 100);
            Reject(() => capture.BindClip(2, 50, 1, 100));
            capture.Read(new float[4]); capture.Read(new float[4]); var saved = capture.StopAndSnapshot();
            Check(calls == 2 && saved.overflow == 1 && saved.callbacks.Length == 1 && saved.duplicateAttachments == 1, "Overflow does not truncate playback.");
            Check(saved.callbacks[0].returned == 0 && saved.callbacks[0].afterBytes == 0, "EOF does not invent advancement.");
        }
    }
    static void FailuresAndConcurrency()
    {
        var failure = new IOException("Artificial callback failure");
        using (var capture = StreamingAudioCapture.Register("fail", 0))
        {
            StreamingAudioCapture.TryAttach("fail", 0, 1, 2, 100, 100, () => 0, _ => throw failure, _ => throw failure);
            Exception seen = null; try { capture.Read(new float[1]); } catch (Exception e) { seen = e; }
            Check(ReferenceEquals(failure, seen), "Read exception preserved.");
            seen = null; try { capture.SetPosition(0); } catch (Exception e) { seen = e; }
            Check(ReferenceEquals(failure, seen), "Set exception preserved.");
            var saved = capture.StopAndSnapshot(); Check(saved.errors == 2 && saved.callbacks[0].failed && saved.callbacks[1].failed, "Failed callbacks recorded as failures.");
        }
        using (var capture = StreamingAudioCapture.Register("position-fail", 0))
        {
            int calls = 0;
            StreamingAudioCapture.TryAttach("position-fail", 0, 1, 2, 100, 100, () => throw failure, _ => ++calls, _ => { });
            capture.Read(new float[1]); Check(calls == 1 && capture.StopAndSnapshot().errors == 1, "Observer failure preserves actual read.");
        }
        using (var entered = new ManualResetEventSlim())
        using (var release = new ManualResetEventSlim())
        using (var capture = StreamingAudioCapture.Register("concurrent", 0))
        {
            StreamingAudioCapture.TryAttach("concurrent", 0, 1, 2, 100, 100, () => 0, _ => { entered.Set(); if (!release.Wait(5000)) throw new TimeoutException(); return 1; }, _ => { });
            Exception threadFailure = null;
            var thread = new Thread(() => { try { capture.Read(new float[1]); } catch (Exception e) { threadFailure = e; } }); thread.Start();
            try
            {
                Check(entered.Wait(5000), "Pending callback entered."); Check(capture.LastCompletedSerial == 0, "Pending data not published.");
                capture.SetPosition(0); var pending = capture.StopAndSnapshot();
                Check(pending.inFlight == 1 && pending.overlaps == 1 && pending.callbacks[0].serial == 0, "Partial/overlapping observation is incomplete.");
            }
            finally { release.Set(); thread.Join(5000); }
            var saved = capture.StopAndSnapshot();
            Check(threadFailure == null && saved.inFlight == 0 && saved.callbacks[0].serial == 1 && saved.reads == 1, "In-flight callback completes after disposal.");
            using (var next = StreamingAudioCapture.Register("next", 0))
            { capture.Dispose(); Check(StreamingAudioCapture.TryAttach("next", 0, 1, 2, 1, 2, () => 0, _ => 0, _ => { }) == next, "Old disposer cannot remove new registration."); }
        }
    }
    public static void Run()
    {
        var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-integrationOutput");
        if (at < 0 || at + 1 >= args.Length) throw new ArgumentException("External output required.");
        string output = args[at + 1]; Directory.CreateDirectory(output); report = new Report(); int exit = 0;
        try { Transparency(); Boundaries(); FailuresAndConcurrency(); report.status = "StreamingAudioCaptureValidated_TestOnly"; }
        catch (Exception e) { report.status = "Failed"; report.failure = e.ToString(); Debug.LogException(e); exit = 1; }
        finally { File.WriteAllText(Path.Combine(output, "integration-summary.json"), JsonUtility.ToJson(report, true)); EditorApplication.Exit(exit); }
    }
}
