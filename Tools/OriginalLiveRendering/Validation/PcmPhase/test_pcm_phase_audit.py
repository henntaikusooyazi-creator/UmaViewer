import copy
import math
import struct

import pytest

from pcm_phase_audit import (
    contain_interval,
    dsp_sample,
    f32,
    integer,
    match_blocks,
    read_phase,
)


def fixture():
    pcm = struct.pack("<"+"f"*512, *(i/1024 for i in range(512)))
    w = {"sourceId": -1, "clipId": -2, "errors": 0, "inFlight": 0, "coveredSamples": 256, "sampleCount": 256, "channels": 2,
             "frequency": 48000, "outputFrequency": 48000, "startSample": 1000, "filterCount": 1,
             "filters": [{"serial": 1, "capturedFrames": 128, "bufferFrames": 128, "firstFloat": 0,
                           "dspTime": 1., "beginTicks": 10, "endTicks": 20}]}
    return w, pcm, bytes([1])*256, pcm[:1024]


def block():
    return {"sourceId": -1, "clipId": -2, "frequency": 48000, "serial": 1, "logical": 1000, "frames": 128, "dsp_sample": 48000, "beginTicks": 10, "endTicks": 20}


def owner():
    return {"sourceId": -1, "clipId": -2, "serial": 1, "submissionSerial": 3, "audioSamples": 1000, "readBeginTicks": 21,
                "readEndTicks": 22, "hasDspTime": True, "readBeginDspTime": 1., "readEndDspTime": 1.,
                "sourceTime": f32(1000/48000)}


def test_whole_block_and_half_open_bounds_are_independent_of_owner():
    w, pcm, coverage, filtered = fixture()
    r = match_blocks(w, pcm, coverage, filtered)
    assert len(r["matches"]) == 1 and not r["refused"]
    assert r["matches"][0]["measured_epoch"] == -47000
    selected = contain_interval(r["matches"], 1000, 1128)
    assert selected["status"] == "SameAssetCoordinateIntervalOnly"
    assert not selected["point_owner_join"] and not selected["original_same_frame_RGB_admitted"]
    assert contain_interval(r["matches"], 1127, 1129)["candidates"] == 0
    assert contain_interval(r["matches"]*2, 1000, 1128)["candidates"] == 2
    assert read_phase(block(), owner(), 48000)["same_dsp_point"]


def test_silence_difference_and_partial_content_are_refused():
    w, pcm, coverage, _ = fixture()
    assert match_blocks(w, pcm, coverage, bytes(1024))["refused"][0]["reason"] == "SilentWindow"
    assert match_blocks(w, pcm, bytes(256), pcm[:1024])["refused"]
    changed = struct.pack("<f", -.25)+pcm[4:1024]
    assert match_blocks(w, pcm, coverage, changed)["refused"][0]["reason"] == "NoExactCoveredContent"


@pytest.mark.parametrize("key,value", [("errors", 1), ("inFlight", 1), ("coveredSamples", 255),
    ("channels", 0), ("outputFrequency", 44100), ("sampleCount", 257), ("filterCount", 0),
    ("startSample", -1), ("frequency", 0), ("sourceId", 0), ("clipId", True)])
def test_window_refusals(key, value):
    w, pcm, c, f = fixture()
    w[key] = value
    with pytest.raises(ValueError):
        match_blocks(w, pcm, c, f)


@pytest.mark.parametrize("key,value", [("serial", 2), ("capturedFrames", 127),
    ("bufferFrames", 1024), ("firstFloat", 1), ("beginTicks", 21), ("dspTime", .000001), ("serial", True)])
def test_filter_refusals(key, value):
    w, pcm, c, f = fixture()
    w["filters"][0][key] = value
    with pytest.raises(ValueError):
        match_blocks(w, pcm, c, f)


def test_storage_limits_and_extraneous_bytes():
    w, pcm, c, f = fixture()
    for binary in (f[:-1], f+b"x", bytearray(f)):
        with pytest.raises(ValueError):
            match_blocks(w, pcm, c, binary)
    w["filters"] = []
    w["filterCount"] = 0
    with pytest.raises(ValueError):
        match_blocks(w, pcm, c, f)


@pytest.mark.parametrize("value", [True, -1, 2**64, .5])
def test_integer_refusals(value):
    with pytest.raises(ValueError):
        integer(value)


@pytest.mark.parametrize("value", [math.nan, math.inf, -1, True, .000001, 2**53])
def test_dsp_refusals(value):
    with pytest.raises(ValueError):
        dsp_sample(value, 48000)


def test_read_phase_orders_are_distinct_and_never_shifted():
    b, o = block(), owner()
    assert read_phase(b, o, 48000)["relation"] == "FilterCopyCompletedBeforeRead"
    o.update(readBeginTicks=1, readEndTicks=2)
    assert read_phase(b, o, 48000)["relation"] == "ReadCompletedBeforeFilterCopy"
    o.update(readBeginTicks=11, readEndTicks=12, readEndDspTime=48128/48000)
    r = read_phase(b, o, 48000)
    assert r["relation"] == "OverlappingCopyObservations" and not r["same_dsp_point"]
    o["audioSamples"] = 1128
    assert read_phase(b, o, 48000) is None


@pytest.mark.parametrize("changes", [{"hasDspTime": False}, {"readBeginTicks": 30},
    {"readBeginDspTime": 2}, {"sourceTime": 1}, {"submissionSerial": 0}])
def test_read_refusals(changes):
    o = owner()
    o.update(changes)
    with pytest.raises(ValueError):
        read_phase(block(), o, 48000)


def test_invalid_filter_clock_and_empty_original_interval():
    b = copy.copy(block())
    b["beginTicks"] = 21
    with pytest.raises(ValueError):
        read_phase(b, owner(), 48000)
    with pytest.raises(ValueError):
        contain_interval([block()], 3, 3)


def test_actual_callback_entry_is_separate_from_copy_start():
    b, o = block(), owner()
    b.update(callbackBeginTicks=5, dspReadEndTicks=8)
    assert read_phase(b, o, 48000)["entry_relation"] == "FilterCopyCompletedBeforeRead"
    o.update(readBeginTicks=1, readEndTicks=2)
    assert read_phase(b, o, 48000)["entry_relation"] == "ReadCompletedBeforeFilterEntry"
    o.update(readBeginTicks=7, readEndTicks=9)
    assert read_phase(b, o, 48000)["entry_relation"] == "ObservedBracketsOverlap"
    b["dspReadEndTicks"] = 11
    with pytest.raises(ValueError):
        read_phase(b, o, 48000)


def test_block_entry_order_is_qualified_and_retained():
    w, pcm, c, f = fixture()
    w["filters"][0].update(callbackBeginTicks=1, dspReadEndTicks=9)
    assert match_blocks(w, pcm, c, f)["matches"][0]["callbackBeginTicks"] == 1
    w["filters"][0]["dspReadEndTicks"] = 11
    with pytest.raises(ValueError):
        match_blocks(w, pcm, c, f)


@pytest.mark.parametrize("key,value", [("sourceId", -3), ("clipId", 0), ("sourceId", True)])
def test_foreign_or_missing_identity_is_rejected(key, value):
    b = block()
    b[key] = value
    with pytest.raises(ValueError):
        read_phase(b, owner(), 48000)


def test_different_sample_units_are_not_shifted():
    with pytest.raises(ValueError):
        read_phase(block(), owner(), 44100)
