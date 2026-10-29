"""Bounded, exact PCM block and read-phase audit; no clock corrections."""
import math
import struct

from pcm_content_match import match_window


def integer(value, minimum=0, maximum=2**63-1):
    if type(value) is not int or not minimum <= value <= maximum:
        raise ValueError("bounded integer required")
    return value


def dsp_sample(seconds, rate):
    if type(seconds) not in (float, int) or not math.isfinite(seconds) or seconds < 0:
        raise ValueError("finite nonnegative DSP time required")
    product = seconds*rate
    if not math.isfinite(product) or product > 2**52 or abs(product-round(product)) > 2*math.ulp(product):
        raise ValueError("observed DSP sample must be integral within floating representation")
    return round(product)


def f32(value):
    return struct.unpack("<f", struct.pack("<f", value))[0]


def read_phase(block, owner, rate):
    """Return an observed relation; interval membership never proves point equality."""
    for key in ("sourceId", "clipId"):
        if integer(block[key], -(2**31), 2**31-1) == 0 or block[key] != owner[key] or type(owner[key]) is not int:
            raise ValueError("same nonzero source/clip identity required within one capture lifetime")
    if integer(rate, 8000, 192000) != block["frequency"]:
        raise ValueError("same sample units required")
    first = integer(block["logical"])
    frames = integer(block["frames"], 128, 2048)
    point = integer(owner["audioSamples"])
    if not first <= point < first+frames:
        return None
    begin, end = integer(owner["readBeginTicks"]), integer(owner["readEndTicks"])
    if begin > end or not owner["hasDspTime"]:
        raise ValueError("ordered explicit read observation required")
    db = dsp_sample(owner["readBeginDspTime"], rate)
    de = dsp_sample(owner["readEndDspTime"], rate)
    if db > de or owner["sourceTime"] != f32(point/rate):
        raise ValueError("DSP order or source/sample point changed during read")
    fb, fe = integer(block["beginTicks"]), integer(block["endTicks"])
    if fb > fe:
        raise ValueError("ordered filter observation required")
    relation = "FilterCopyCompletedBeforeRead" if fe <= begin else "ReadCompletedBeforeFilterCopy" if end <= fb else "OverlappingCopyObservations"
    entry_relation = "NotObserved"
    if "callbackBeginTicks" in block:
        entry = integer(block["callbackBeginTicks"])
        dsp_end = integer(block["dspReadEndTicks"])
        if not entry <= dsp_end <= fb:
            raise ValueError("filter entry/DSP/copy ordering invalid")
        entry_relation = "ReadCompletedBeforeFilterEntry" if end <= entry else "FilterCopyCompletedBeforeRead" if fe <= begin else "ObservedBracketsOverlap"
    return {"observation": integer(owner["serial"], 1), "submission": integer(owner["submissionSerial"], 1),
                "sample": point, "filter_serial": block["serial"], "filter_logical": first,
                "filter_frames": frames, "relation": relation, "entry_relation": entry_relation, "dsp_begin_sample": db, "dsp_end_sample": de,
                "same_dsp_point": (db == de == block["dsp_sample"]+point-first)}


def match_blocks(window, provider, coverage, filtered):
    if window["errors"] or window["inFlight"] or window["coveredSamples"] != window["sampleCount"]:
        raise ValueError("complete drained source window required")
    channels = integer(window["channels"], 1, 8)
    rate = integer(window["frequency"], 8000, 192000)
    if rate != window["outputFrequency"]:
        raise ValueError("resampled coordinate units are not qualified")
    count = integer(window["sampleCount"], 128, min(4*rate, 1048576//channels))
    start = integer(window["startSample"])
    for key in ("sourceId", "clipId"):
        if integer(window[key], -(2**31), 2**31-1) == 0:
            raise ValueError("explicit nonzero source/clip identity required")
    rows = window["filters"]
    if (len(provider) != count*channels*4 or len(coverage) != count or
            not isinstance(filtered, bytes) or not 0 < len(filtered) <= 4194304 or
            window["filterCount"] != len(rows) or not 0 < len(rows) <= 256):
        raise ValueError("bounded complete block storage required")
    matches, refused, cursor = [], [], 0
    for index, row in enumerate(rows):
        frames = integer(row["capturedFrames"], 128, 2048)
        if integer(row["serial"], 1) != index+1 or integer(row["bufferFrames"]) != frames or integer(row["firstFloat"]) != cursor:
            raise ValueError("full ordered filter blocks required")
        size = frames*channels*4
        needle = filtered[cursor*4:cursor*4+size]
        if len(needle) != size:
            raise ValueError("filter block truncated")
        cursor += frames*channels
        begin, end = integer(row["beginTicks"]), integer(row["endTicks"])
        if begin > end:
            raise ValueError("filter timing reversed")
        dsp = dsp_sample(row["dspTime"], rate)
        at, reason = match_window(provider, coverage, needle, channels)
        if at is None:
            refused.append({"serial": row["serial"], "reason": reason})
        else:
            logical = start+at
            matched = {"serial": row["serial"], "logical": logical, "frames": frames, "dsp_sample": dsp,
                       "measured_epoch": logical-dsp, "beginTicks": begin, "endTicks": end,
                       "sourceId": window["sourceId"], "clipId": window["clipId"], "frequency": rate}
            if "callbackBeginTicks" in row:
                entry = integer(row["callbackBeginTicks"])
                dsp_end = integer(row["dspReadEndTicks"])
                if not entry <= dsp_end <= begin:
                    raise ValueError("filter entry/DSP/copy ordering invalid")
                matched.update(callbackBeginTicks=entry, dspReadEndTicks=dsp_end)
            matches.append(matched)
    if cursor*4 != len(filtered):
        raise ValueError("unreferenced filter bytes")
    return {"start": start, "samples": count, "rate": rate, "matches": matches, "refused": refused}


def contain_interval(matches, begin, end):
    begin, end = integer(begin), integer(end)
    if begin >= end:
        raise ValueError("nonempty half-open original interval required")
    selected = [r for r in matches if r["logical"] <= begin and end <= r["logical"]+r["frames"]]
    if len(selected) != 1:
        return {"status": "NoUniqueContainingBlock", "candidates": len(selected)}
    row = selected[0]
    return {"status": "SameAssetCoordinateIntervalOnly", "filter_serial": row["serial"],
                "original_begin": begin, "original_end": end, "filter_begin": row["logical"],
                "filter_end": row["logical"]+row["frames"], "point_owner_join": False,
                "original_same_frame_RGB_admitted": False}
