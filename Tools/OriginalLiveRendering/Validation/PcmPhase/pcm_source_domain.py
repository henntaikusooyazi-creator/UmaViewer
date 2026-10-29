"""Exact float32 sample preimage, conditional on the observed sample domain."""
import struct
from fractions import Fraction


def exact_sample_preimage(word, rate, total):
    if (type(word) is not int or not 0 <= word < 0x7f800000 or type(rate) is not int or
            not 8000 <= rate <= 192000 or type(total) is not int or not 0 < total <= 2**31-1):
        raise ValueError("finite nonnegative float32 and explicit bounded sample units required")
    def value(bits):
        return Fraction.from_float(struct.unpack("<f", struct.pack("<I", bits))[0])
    current = value(word)
    lower = (value(word-1)+current)/2 if word else Fraction(0)
    upper = (value(word+1)+current)/2 if word < 0x7f7fffff else current
    lower *= rate
    upper *= rate
    lo = -((-lower.numerator)//lower.denominator)
    hi = upper.numerator//upper.denominator
    if word & 1:
        lo += lower.denominator == 1
        hi -= upper.denominator == 1
    lo, hi = max(0, lo), min(total-1, hi)
    return (lo, hi) if lo <= hi else None


def compare_observed(word, rate, total, positions):
    if not isinstance(positions, list) or not 0 < len(positions) <= 16384 or any(
            type(n) is not int or not 0 <= n < total for n in positions):
        raise ValueError("bounded actual observed sample positions required")
    possible = exact_sample_preimage(word, rate, total)
    selected = [] if possible is None else [i for i, n in enumerate(positions) if possible[0] <= n <= possible[1]]
    return {"scope": "ExactFloat32Preimage_ObservedPositionsOnly_NotEngineSamplingGuarantee",
            "sample_preimage": possible, "observed_matches": selected,
            "source_to_viewer_clock_contract": False}
