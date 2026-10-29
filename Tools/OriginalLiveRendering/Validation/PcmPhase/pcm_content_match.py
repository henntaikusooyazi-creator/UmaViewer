"""Exact non-silent PCM content lineage, never nearest-time matching."""
import math
import struct


def match_window(provider, coverage, needle, channels):
    if type(channels) is not int or not 1 <= channels <= 8:
        raise ValueError("explicit channels required")
    unit = channels*4
    if (not isinstance(provider, bytes) or not 0 < len(provider) <= 4*1024*1024
            or len(provider)%unit or not isinstance(needle, bytes) or not 0 < len(needle) <= 131072
            or len(needle)%unit or len(coverage) != len(provider)//unit or any(b not in (0, 1) for b in coverage)):
        raise ValueError("bounded whole-frame PCM and coverage required")
    values = [v[0] for v in struct.iter_unpack("<f", needle)]
    if not all(math.isfinite(v) for v in values):
        raise ValueError("finite PCM required")
    if not any(v != 0 for v in values):
        return None, "SilentWindow"
    selected = []
    start = 0
    while True:
        at = provider.find(needle, start)
        if at < 0:
            break
        if at%unit == 0 and all(coverage[at//unit:(at+len(needle))//unit]):
            selected.append(at//unit)
            if len(selected) > 1:
                return None, "AmbiguousContent"
        start = at+1
    return (selected[0], "UniqueExactPcm") if selected else (None, "NoExactCoveredContent")
