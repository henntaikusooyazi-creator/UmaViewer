import struct

import pytest

from pcm_content_match import match_window


def data(*values):
    return struct.pack("<"+"f"*len(values), *values)


def test_unique_byte_content_is_not_a_time_or_silence_guess():
    body = data(0, 0, .125, -.25, .5, -.75, 0, 0)
    assert match_window(body, bytes([1]*4), body[8:24], 2) == (1, "UniqueExactPcm")
    assert match_window(body, bytes([1]*4), data(0, -0.0), 2) == (None, "SilentWindow")
    assert match_window(body, bytes([1]*4), data(.126, -.25), 2) == (None, "NoExactCoveredContent")


def test_repeated_or_unobserved_content_is_not_a_coordinate():
    block = data(.25, -.5)
    assert match_window(block*2, bytes([1, 1]), block, 2) == (None, "AmbiguousContent")
    assert match_window(block*2, bytes([0, 1]), block, 2) == (1, "UniqueExactPcm")
    assert match_window(block, bytes([0]), block, 2) == (None, "NoExactCoveredContent")


def test_channel_frame_alignment_is_mandatory():
    block = data(.25, -.5)
    body = data(1)+block+data(2)
    assert match_window(body, bytes([1, 1]), block, 2) == (None, "NoExactCoveredContent")


@pytest.mark.parametrize("channels", [0, 9, True, 1.0])
def test_invalid_channel_units(channels):
    with pytest.raises(ValueError):
        match_window(data(1), b"\1", data(1), channels)


@pytest.mark.parametrize("provider,coverage,needle", [
    (b"", b"", data(1)), (data(1), b"", data(1)),
    (data(1), b"\2", data(1)), (data(1), b"\1", b"x"),
    (data(1), b"\1", data(float("nan"))), (data(1), b"\1", data(float("inf"))),
    (data(1), b"\1", b""), (data(1), b"\1", bytes(131076)),
    (bytearray(data(1)), b"\1", data(1))], ids=[
        "empty-provider", "missing-coverage", "invalid-coverage", "unaligned-needle",
        "nan", "infinity", "empty-needle", "oversize-needle", "mutable-provider"])
def test_invalid_binary_extents(provider, coverage, needle):
    with pytest.raises(ValueError):
        match_window(provider, coverage, needle, 1)
